using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Workflow.Handlers;

public sealed class DelayStepHandler : IWorkflowStepHandler
{
    public string StepType => "delay";

    public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, WorkflowRunEntity run, WorkflowDefinition definition, CancellationToken ct = default)
    {
        var seconds = step.DelaySeconds ?? 0;
        var next = step.Next ?? NextSequential(definition, step.Id);
        return Task.FromResult(new StepExecutionResult(
            NextStepId: next,
            ContinueAt: DateTimeOffset.UtcNow.AddSeconds(seconds),
            Completed: false,
            Failed: false,
            EventType: "delayed",
            EventMessage: $"Delayed {seconds}s",
            EventData: new { seconds, next }
        ));
    }

    private static string? NextSequential(WorkflowDefinition def, string currentId)
    {
        var idx = def.Steps.FindIndex(s => s.Id == currentId);
        return idx >= 0 && idx + 1 < def.Steps.Count ? def.Steps[idx + 1].Id : null;
    }
}
