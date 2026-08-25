using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Workflow;

/// <summary>Persistence for workflow definitions and runs (SRP).</summary>
public interface IWorkflowRunRepository
{
    Task SaveDefinitionAsync(WorkflowDefinition definition, CancellationToken ct = default);
    Task<WorkflowDefinition?> GetDefinitionAsync(string key, string? tenantId, CancellationToken ct = default);
    Task<Guid> CreateRunAsync(WorkflowRunEntity run, CancellationToken ct = default);
    Task<WorkflowRunEntity?> GetRunAsync(Guid runId, CancellationToken ct = default);
    Task UpdateRunAsync(WorkflowRunEntity run, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowRunEntity>> GetDueRunsAsync(DateTimeOffset now, int take, CancellationToken ct = default);
    Task<WorkflowRunStatusDto?> GetRunStatusAsync(Guid runId, CancellationToken ct = default);
}
