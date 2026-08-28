using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Orchestration;

namespace NotificationHub.Application.Features.Notifications.SendSync;

public sealed class SendNotificationSyncHandler(NotificationOrchestrator orchestrator)
    : IRequestHandler<SendNotificationSyncCommand, Result<DeliveryResult>>
{
    public async Task<Result<DeliveryResult>> Handle(SendNotificationSyncCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        if (!string.IsNullOrEmpty(request.TrustedTenantId))
            req = req with { TenantId = request.TrustedTenantId };

        var result = await orchestrator.SendAsync(req, cancellationToken);
        return Result.Success(result);
    }
}
