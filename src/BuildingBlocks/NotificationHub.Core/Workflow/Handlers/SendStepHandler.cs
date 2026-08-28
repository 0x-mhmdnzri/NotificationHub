using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Queue;

namespace NotificationHub.Core.Workflow.Handlers;

public sealed class SendStepHandler : IWorkflowStepHandler
{
    private readonly IServiceScopeFactory _scopeFactory;
    public SendStepHandler(IServiceScopeFactory scopeFactory) => _scopeFactory = scopeFactory;
    public string StepType => "send";

    public async Task<StepExecutionResult> ExecuteAsync(WorkflowStep step, WorkflowRunEntity run, WorkflowDefinition definition, CancellationToken ct = default)
    {
        var data = JsonSerializer.Deserialize<Dictionary<string, object?>>(run.DataJson) ?? new();
        using var scope = _scopeFactory.CreateScope();
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

        var next = step.Next ?? NextSequential(definition, step.Id);
        return new StepExecutionResult(
            NextStepId: next,
            ContinueAt: DateTimeOffset.UtcNow,
            Completed: string.IsNullOrEmpty(next),
            Failed: false,
            EventType: "sent",
            EventMessage: $"Queued notification {status.NotificationId} via {request.Channel}",
            EventData: new { notificationId = status.NotificationId, channel = request.Channel, status = status.Status.ToString(), next }
        );
    }

    private static string? NextSequential(WorkflowDefinition def, string currentId)
    {
        var idx = def.Steps.FindIndex(s => s.Id == currentId);
        return idx >= 0 && idx + 1 < def.Steps.Count ? def.Steps[idx + 1].Id : null;
    }
}
