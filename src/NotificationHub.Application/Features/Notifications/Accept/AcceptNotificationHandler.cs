using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Orchestration;

namespace NotificationHub.Application.Features.Notifications.Accept;

/// <summary>
/// Orchestrates accept path. Delivery side-effects go through existing outbox pipeline inside orchestrator.
/// Does not call other commands (no command chaining).
/// </summary>
public sealed class AcceptNotificationHandler(NotificationOrchestrator orchestrator)
    : IRequestHandler<AcceptNotificationCommand, Result<AcceptNotificationResponse>>
{
    public async Task<Result<AcceptNotificationResponse>> Handle(
        AcceptNotificationCommand request,
        CancellationToken cancellationToken)
    {
        var req = request.Request;
        if (!string.IsNullOrEmpty(request.TrustedTenantId))
            req = req with { TenantId = request.TrustedTenantId };

        var (accepted, status) = await orchestrator.AcceptAsync(req, cancellationToken);

        if (!accepted)
            return Result.Failure<AcceptNotificationResponse>(
                Error.Failure("notification.not_accepted", status.ErrorMessage ?? "Not accepted"));

        return Result.Success(new AcceptNotificationResponse(
            status.NotificationId,
            status.Status.ToString(),
            status.ErrorMessage));
    }
}
