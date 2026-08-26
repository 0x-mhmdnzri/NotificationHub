using MediatR;
using NotificationHub.Application.Common.Interfaces;
using NotificationHub.Core.Orchestration;

namespace NotificationHub.Application.Notifications.Commands.AcceptNotification;

public sealed class AcceptNotificationCommandHandler(NotificationOrchestrator orchestrator)
    : IRequestHandler<AcceptNotificationCommand, AcceptNotificationResult>
{
    public async Task<AcceptNotificationResult> Handle(AcceptNotificationCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        if (!string.IsNullOrEmpty(request.ResolvedTenantId))
            req = req with { TenantId = request.ResolvedTenantId };

        var (accepted, status) = await orchestrator.AcceptAsync(req, cancellationToken);
        return new AcceptNotificationResult(accepted, status);
    }
}
