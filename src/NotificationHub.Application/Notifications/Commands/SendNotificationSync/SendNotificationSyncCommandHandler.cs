using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Orchestration;

namespace NotificationHub.Application.Notifications.Commands.SendNotificationSync;

public sealed class SendNotificationSyncCommandHandler(NotificationOrchestrator orchestrator)
    : IRequestHandler<SendNotificationSyncCommand, DeliveryResult>
{
    public async Task<DeliveryResult> Handle(SendNotificationSyncCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        if (!string.IsNullOrEmpty(request.ResolvedTenantId))
            req = req with { TenantId = request.ResolvedTenantId };
        return await orchestrator.SendAsync(req, cancellationToken);
    }
}
