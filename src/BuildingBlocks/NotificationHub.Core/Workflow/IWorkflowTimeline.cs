using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Workflow;

/// <summary>Append-only timeline of workflow run events (SRP).</summary>
public interface IWorkflowTimeline
{
    Task AppendAsync(Guid runId, string eventType, string? stepId = null, string? message = null, object? data = null, CancellationToken ct = default);
    Task<IReadOnlyList<WorkflowTimelineEventDto>> GetTimelineAsync(Guid runId, CancellationToken ct = default);
}
