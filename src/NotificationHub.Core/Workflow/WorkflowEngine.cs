using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Common;

namespace NotificationHub.Core.Workflow;

public interface IWorkflowEngine
{
    Task<WorkflowDefinition> SaveAsync(WorkflowDefinition definition, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetAsync(string key, string? tenantId = null, CancellationToken ct = default);
    Task<Guid> StartAsync(WorkflowStartRequest request, CancellationToken ct = default);
    Task ProcessDueRunsAsync(CancellationToken ct = default);
    Task<WorkflowRunStatusDto?> GetRunAsync(Guid runId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTimelineEventDto>> GetTimelineAsync(Guid runId, CancellationToken ct = default);
    Task<bool> CancelAsync(Guid runId, CancellationToken ct = default);
}

/// <summary>
/// Orchestrates workflow runs. Depends on repository, timeline, and step handlers (DIP/OCP).
/// </summary>
public sealed class WorkflowEngine : IWorkflowEngine
{
    private readonly IWorkflowRunRepository _repo;
    private readonly IWorkflowTimeline _timeline;
    private readonly IReadOnlyDictionary<string, IWorkflowStepHandler> _handlers;
    private readonly ILogger<WorkflowEngine> _logger;

    public WorkflowEngine(
        IWorkflowRunRepository repo,
        IWorkflowTimeline timeline,
        IEnumerable<IWorkflowStepHandler> handlers,
        ILogger<WorkflowEngine> logger)
    {
        _repo = repo;
        _timeline = timeline;
        _handlers = handlers.ToDictionary(h => h.StepType, h => h, StringComparer.OrdinalIgnoreCase);
        _logger = logger;
    }

    public async Task<WorkflowDefinition> SaveAsync(WorkflowDefinition definition, CancellationToken ct = default)
    {
        var id = await _repo.SaveDefinitionAsync(definition, ct);
        return definition with { Id = id };
    }

    public Task<WorkflowDefinition?> GetAsync(string key, string? tenantId = null, CancellationToken ct = default)
        => _repo.GetDefinitionAsync(key, tenantId, ct);

    public async Task<Guid> StartAsync(WorkflowStartRequest request, CancellationToken ct = default)
    {
        var def = await _repo.GetDefinitionAsync(request.WorkflowKey, request.TenantId, ct)
                  ?? throw new InvalidOperationException($"Workflow '{request.WorkflowKey}' not found");
        if (!def.IsActive) throw new InvalidOperationException("Workflow is inactive");
        if (def.Steps.Count == 0) throw new InvalidOperationException("Workflow has no steps");

        var run = new WorkflowRunEntity
        {
            Id = ServerIds.New(),
            WorkflowId = def.Id,
            WorkflowKey = def.Key,
            Recipient = request.Recipient,
            TenantId = request.TenantId,
            Status = "running",
            CurrentStepId = def.Steps[0].Id,
            DataJson = JsonSerializer.Serialize(request.Data),
            ContinueAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        await _repo.CreateRunAsync(run, ct);
        await _timeline.AppendAsync(run.Id, "started", def.Steps[0].Id, $"Started workflow {def.Key}", new { request.Recipient, request.WorkflowKey }, ct);
        _logger.LogInformation("Started workflow {Key} run {RunId}", def.Key, run.Id);
        return run.Id;
    }

    public async Task ProcessDueRunsAsync(CancellationToken ct = default)
    {
        var runs = await _repo.GetDueRunsAsync(DateTimeOffset.UtcNow, 50, ct);
        foreach (var run in runs)
        {
            try { await AdvanceAsync(run, ct); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Workflow run {RunId} failed", run.Id);
                run.Status = "failed";
                run.LastError = ex.Message;
                await _repo.UpdateRunAsync(run, ct);
                await _timeline.AppendAsync(run.Id, "failed", run.CurrentStepId, ex.Message, ct: ct);
            }
        }
    }

    public Task<WorkflowRunStatusDto?> GetRunAsync(Guid runId, CancellationToken ct = default)
        => _repo.GetRunStatusAsync(runId, ct);

    public Task<IReadOnlyList<WorkflowTimelineEventDto>> GetTimelineAsync(Guid runId, CancellationToken ct = default)
        => _timeline.GetTimelineAsync(runId, ct);

    public async Task<bool> CancelAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _repo.GetRunAsync(runId, ct);
        if (run is null) return false;
        if (run.Status is "completed" or "failed" or "cancelled") return false;
        run.Status = "cancelled";
        await _repo.UpdateRunAsync(run, ct);
        await _timeline.AppendAsync(runId, "cancelled", run.CurrentStepId, "Cancelled by API", ct: ct);
        return true;
    }

    private async Task AdvanceAsync(WorkflowRunEntity run, CancellationToken ct)
    {
        var def = await _repo.GetDefinitionAsync(run.WorkflowKey, run.TenantId, ct);
        if (def is null)
        {
            run.Status = "failed";
            run.LastError = "Workflow definition missing";
            await _repo.UpdateRunAsync(run, ct);
            await _timeline.AppendAsync(run.Id, "failed", null, run.LastError, ct: ct);
            return;
        }

        var step = def.Steps.FirstOrDefault(s => s.Id == run.CurrentStepId);
        if (step is null)
        {
            run.Status = "completed";
            await _repo.UpdateRunAsync(run, ct);
            await _timeline.AppendAsync(run.Id, "completed", null, "No more steps", ct: ct);
            return;
        }

        if (!_handlers.TryGetValue(step.Type, out var handler))
        {
            run.Status = "failed";
            run.LastError = $"Unknown step type '{step.Type}'";
            await _repo.UpdateRunAsync(run, ct);
            await _timeline.AppendAsync(run.Id, "failed", step.Id, run.LastError, ct: ct);
            return;
        }

        await _timeline.AppendAsync(run.Id, "step_entered", step.Id, $"Entering {step.Type}", ct: ct);
        var result = await handler.ExecuteAsync(step, run, def, ct);

        if (result.EventType is not null)
            await _timeline.AppendAsync(run.Id, result.EventType, step.Id, result.EventMessage, result.EventData, ct);

        if (result.Failed)
        {
            run.Status = "failed";
            run.LastError = result.Error;
            await _repo.UpdateRunAsync(run, ct);
            await _timeline.AppendAsync(run.Id, "failed", step.Id, result.Error, ct: ct);
            return;
        }

        await _timeline.AppendAsync(run.Id, "step_completed", step.Id, $"Completed {step.Type}", ct: ct);

        run.CurrentStepId = result.NextStepId;
        run.ContinueAt = result.ContinueAt ?? DateTimeOffset.UtcNow;

        if (result.Completed || string.IsNullOrEmpty(result.NextStepId))
        {
            run.Status = "completed";
            run.CurrentStepId = null;
            await _repo.UpdateRunAsync(run, ct);
            await _timeline.AppendAsync(run.Id, "completed", step.Id, "Workflow completed", ct: ct);
            return;
        }

        await _repo.UpdateRunAsync(run, ct);
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
