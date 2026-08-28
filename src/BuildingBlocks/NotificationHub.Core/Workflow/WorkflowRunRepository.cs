using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Common;

namespace NotificationHub.Core.Workflow;

public sealed class WorkflowRunRepository : IWorkflowRunRepository
{
    private readonly NotificationDbContext _db;
    public WorkflowRunRepository(NotificationDbContext db) => _db = db;

    public async Task<Guid> SaveDefinitionAsync(WorkflowDefinition definition, CancellationToken ct = default)
    {
        var entity = await _db.Workflows.FirstOrDefaultAsync(x => x.Key == definition.Key && x.TenantId == definition.TenantId, ct);
        if (entity is null)
        {
            entity = new WorkflowDefinitionEntity { Id = ServerIds.New(), Key = definition.Key, TenantId = definition.TenantId };
            _db.Workflows.Add(entity);
        }
        entity.IsActive = definition.IsActive;
        entity.StepsJson = JsonSerializer.Serialize(definition.Steps);
        if (entity.CreatedAt == default)
            entity.CreatedAt = definition.CreatedAt == default ? DateTimeOffset.UtcNow : definition.CreatedAt;
        await _db.SaveChangesAsync(ct);
        return entity.Id;
    }

    public async Task<WorkflowDefinition?> GetDefinitionAsync(string key, string? tenantId, CancellationToken ct = default)
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

    public async Task<Guid> CreateRunAsync(WorkflowRunEntity run, CancellationToken ct = default)
    {
        _db.WorkflowRuns.Add(run);
        await _db.SaveChangesAsync(ct);
        return run.Id;
    }

    public Task<WorkflowRunEntity?> GetRunAsync(Guid runId, CancellationToken ct = default)
        => _db.WorkflowRuns.FirstOrDefaultAsync(x => x.Id == runId, ct)!;

    public async Task UpdateRunAsync(WorkflowRunEntity run, CancellationToken ct = default)
    {
        run.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<WorkflowRunEntity>> GetDueRunsAsync(DateTimeOffset now, int take, CancellationToken ct = default)
        => await _db.WorkflowRuns
            .Where(x => x.Status == "running" && (x.ContinueAt == null || x.ContinueAt <= now))
            .OrderBy(x => x.ContinueAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<WorkflowRunStatusDto?> GetRunStatusAsync(Guid runId, CancellationToken ct = default)
    {
        var e = await _db.WorkflowRuns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == runId, ct);
        if (e is null) return null;
        return new WorkflowRunStatusDto
        {
            RunId = e.Id, WorkflowId = e.WorkflowId, WorkflowKey = e.WorkflowKey, Recipient = e.Recipient,
            TenantId = e.TenantId, Status = e.Status, CurrentStepId = e.CurrentStepId,
            ContinueAt = e.ContinueAt, CreatedAt = e.CreatedAt, UpdatedAt = e.UpdatedAt, LastError = e.LastError
        };
    }
}
