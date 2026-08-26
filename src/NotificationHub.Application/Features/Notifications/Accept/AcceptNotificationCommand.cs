using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Notifications.Accept;

/// <summary>Intent: accept a notification for async delivery (queue/outbox).</summary>
[AuthorizeRoles(AppRoles.Admin, AppRoles.Sender)]
public sealed record AcceptNotificationCommand(
    NotificationRequest Request,
    string? TrustedTenantId
) : ICommand<Result<AcceptNotificationResponse>>;

public sealed record AcceptNotificationResponse(
    Guid NotificationId,
    string Status,
    string? Reason
);
