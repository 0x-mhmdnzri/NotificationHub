using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Workflow.Handlers;

public sealed class BranchStepHandler : IWorkflowStepHandler
{
    private readonly ConditionStepHandler _inner = new();
    public string StepType => "branch";
    public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, WorkflowRunEntity run, WorkflowDefinition definition, CancellationToken ct = default)
        => _inner.ExecuteAsync(step, run, definition, ct);
}
