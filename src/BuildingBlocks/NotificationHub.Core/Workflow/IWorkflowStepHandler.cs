using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Workflow;

public sealed record StepExecutionResult(
    string? NextStepId,
    DateTimeOffset? ContinueAt,
    bool Completed,
    bool Failed,
    string? Error = null,
    string? EventType = null,
    string? EventMessage = null,
    object? EventData = null);

/// <summary>One handler per step type — open for extension (OCP).</summary>
public interface IWorkflowStepHandler
{
    string StepType { get; }
    Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, WorkflowRunEntity run, WorkflowDefinition definition, CancellationToken ct = default);
}
