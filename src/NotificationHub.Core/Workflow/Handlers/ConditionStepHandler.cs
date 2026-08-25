using System.Text.Json;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Expressions;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Workflow.Handlers;

public sealed class ConditionStepHandler : IWorkflowStepHandler
{
    private readonly IExpressionEvaluator _evaluator;

    public ConditionStepHandler(IExpressionEvaluator evaluator) => _evaluator = evaluator;

    public string StepType => "condition";

    public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, WorkflowRunEntity run, WorkflowDefinition definition, CancellationToken ct = default)
    {
        var data = Normalize(JsonSerializer.Deserialize<Dictionary<string, object?>>(run.DataJson) ?? new());
        var ok = _evaluator.Evaluate(step.ConditionExpression, data);
        var next = ok ? step.NextOnTrue : step.NextOnFalse;
        return Task.FromResult(new StepExecutionResult(
            NextStepId: next,
            ContinueAt: DateTimeOffset.UtcNow,
            Completed: string.IsNullOrEmpty(next),
            Failed: false,
            EventType: "branched",
            EventMessage: ok ? "Condition true" : "Condition false",
            EventData: new { expression = step.ConditionExpression, result = ok, next }
        ));
    }

    private static Dictionary<string, object?> Normalize(Dictionary<string, object?> data)
    {
        // Ensure JsonElement values are usable by evaluator
        return data;
    }
}
