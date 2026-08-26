using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Common.Interfaces;

namespace NotificationHub.Application.Notifications.Commands.AcceptNotification;

public sealed record AcceptNotificationCommand(
    NotificationRequest Request,
    string? ResolvedTenantId
) : ICommand<AcceptNotificationResult>;

public sealed record AcceptNotificationResult(
    bool Accepted,
    NotificationStatus Status
);
