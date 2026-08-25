using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Queue;

namespace NotificationHub.Core.Workflow;

public interface IWorkflowEngine
{
    Task<WorkflowDefinition> SaveAsync(WorkflowDefinition definition, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetAsync(string key, string? tenantId = null, CancellationToken ct = default);
    Task<Guid> StartAsync(WorkflowStartRequest request, CancellationToken ct = default);
    Task ProcessDueRunsAsync(CancellationToken ct = default);
}

public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly NotificationDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(NotificationDbContext db, IServiceScopeFactory scopeFactory, ILogger<WorkflowEngine> logger)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<WorkflowDefinition> SaveAsync(WorkflowDefinition definition, CancellationToken ct = default)
    {
        var entity = await _db.Workflows.FirstOrDefaultAsync(x => x.Key == definition.Key && x.TenantId == definition.TenantId, ct);
        if (entity is null)
        {
            entity = new WorkflowDefinitionEntity { Id = definition.Id, Key = definition.Key, TenantId = definition.TenantId };
            _db.Workflows.Add(entity);
        }
        entity.IsActive = definition.IsActive;
        entity.StepsJson = JsonSerializer.Serialize(definition.Steps);
        entity.CreatedAt = definition.CreatedAt;
        await _db.SaveChangesAsync(ct);
        return definition;
    }

    public async Task<WorkflowDefinition?> GetAsync(string key, string? tenantId = null, CancellationToken ct = default)
    {
        var q = _db.Workflows.AsNoTracking().Where(x => x.Key == key);
        q = tenantId is null ? q.Where(x => x.TenantId == null) : q.Where(x => x.TenantId == tenantId);
        var e = await q.FirstOrDefaultAsync(ct);
        if (e is null) return null;
        return new WorkflowDefinition
        {
            Id = e.Id, Key = e.Key, TenantId = e.TenantId, IsActive = e.IsActive, CreatedAt = e.CreatedAt,
            Steps = JsonSerializer.Deserialize<List<WorkflowStep>>(e.StepsJson) ?? []
        };
    }

    public async Task<Guid> StartAsync(WorkflowStartRequest request, CancellationToken ct = default)
    {
        var def = await GetAsync(request.WorkflowKey, request.TenantId, ct)
                  ?? throw new InvalidOperationException($"Workflow '{request.WorkflowKey}' not found");
        if (!def.IsActive) throw new InvalidOperationException("Workflow is inactive");
        if (def.Steps.Count == 0) throw new InvalidOperationException("Workflow has no steps");

        var run = new WorkflowRunEntity
        {
            WorkflowId = def.Id,
            WorkflowKey = def.Key,
            Recipient = request.Recipient,
            TenantId = request.TenantId,
            Status = "running",
            CurrentStepId = def.Steps[0].Id,
            DataJson = JsonSerializer.Serialize(request.Data),
            ContinueAt = DateTimeOffset.UtcNow
        };
        _db.WorkflowRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Started workflow {Key} run {RunId}", def.Key, run.Id);
        return run.Id;
    }

    public async Task ProcessDueRunsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var runs = await _db.WorkflowRuns
            .Where(x => x.Status == "running" && (x.ContinueAt == null || x.ContinueAt <= now))
            .OrderBy(x => x.ContinueAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var run in runs)
        {
            try
            {
                await AdvanceAsync(run, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow run {RunId} failed", run.Id);
                run.Status = "failed";
                run.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
            }
        }
    }

    private async Task AdvanceAsync(WorkflowRunEntity run, CancellationToken ct)
    {
        var def = await GetAsync(run.WorkflowKey, run.TenantId, ct);
        if (def is null)
        {
            run.Status = "failed";
            await _db.SaveChangesAsync(ct);
            return;
        }

        var step = def.Steps.FirstOrDefault(s => s.Id == run.CurrentStepId);
        if (step is null)
        {
            run.Status = "completed";
            run.UpdatedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
            return;
        }

        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(run.DataJson) ?? new();

        switch (step.Type.ToLowerInvariant())
        {
            case "delay":
                run.ContinueAt = DateTimeOffset.UtcNow.AddSeconds(step.DelaySeconds ?? 0);
                run.CurrentStepId = step.Next ?? NextSequential(def, step.Id);
                run.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                break;

            case "condition":
            case "branch":
                var ok = EvaluateCondition(step.ConditionExpression, data);
                run.CurrentStepId = ok ? step.NextOnTrue : step.NextOnFalse;
                if (string.IsNullOrEmpty(run.CurrentStepId))
                    run.Status = "completed";
                run.ContinueAt = DateTimeOffset.UtcNow;
                run.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                break;

            case "send":
                using (var scope = _scopeFactory.CreateScope())
                {
                    var orchestrator = scope.ServiceProvider.GetRequiredService<NotificationOrchestrator>();
                    var queue = scope.ServiceProvider.GetRequiredService<INotificationQueue>();
                    var request = new NotificationRequest
                    {
                        Recipient = run.Recipient,
                        Channel = step.Channel ?? "email",
                        TemplateKey = step.TemplateKey ?? "welcome",
                        TenantId = run.TenantId,
                        PreferredProvider = step.PreferredProvider,
                        Data = data,
                        CorrelationId = run.Id.ToString()
                    };
                    var (accepted, status) = await orchestrator.AcceptAsync(request, ct);
                    if (accepted && status.Status == DeliveryStatus.Queued)
                        await queue.EnqueueAsync(request, ct);
                }
                run.CurrentStepId = step.Next ?? NextSequential(def, step.Id);
                if (string.IsNullOrEmpty(run.CurrentStepId))
                    run.Status = "completed";
                run.ContinueAt = DateTimeOffset.UtcNow;
                run.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                break;

            default:
                run.Status = "failed";
                run.UpdatedAt = DateTimeOffset.UtcNow;
                await _db.SaveChangesAsync(ct);
                break;
        }
    }

    private static string? NextSequential(WorkflowDefinition def, string currentId)
    {
        var idx = def.Steps.FindIndex(s => s.Id == currentId);
        if (idx < 0 || idx + 1 >= def.Steps.Count) return null;
        return def.Steps[idx + 1].Id;
    }

    private static bool EvaluateCondition(string? expression, Dictionary<string, object?> data)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;
        // simple: key == value | key != value
        var parts = expression.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 3) return false;
        data.TryGetValue(parts[0], out var raw);
        var left = raw?.ToString() ?? "";
        var right = parts[2].Trim('"');
        return parts[1] switch
        {
            "==" => string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            "!=" => !string.Equals(left, right, StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }
}

public sealed class WorkflowBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowBackgroundWorker> _logger;

    public WorkflowBackgroundWorker(IServiceScopeFactory scopeFactory, ILogger<WorkflowBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Workflow worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<IWorkflowEngine>();
                await engine.ProcessDueRunsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow worker tick failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }
    }
}
