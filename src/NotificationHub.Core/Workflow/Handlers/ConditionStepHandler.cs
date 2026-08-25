using System.Text.Json;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Workflow.Handlers;

public sealed class ConditionStepHandler : IWorkflowStepHandler
{
    public string StepType => "condition";

    public Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, WorkflowRunEntity run, WorkflowDefinition definition, CancellationToken ct = default)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(run.DataJson) ?? new();
        var ok = Evaluate(step.ConditionExpression, data);
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

    internal static bool Evaluate(string? expression, Dictionary<string, object?> data)
    {
        if (string.IsNullOrWhiteSpace(expression)) return true;
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
