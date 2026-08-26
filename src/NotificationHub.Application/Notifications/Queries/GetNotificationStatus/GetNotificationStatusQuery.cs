using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Common.Interfaces;
using NotificationHub.Application.Common.Models;

namespace NotificationHub.Application.Notifications.Queries.GetNotificationStatus;

public sealed record GetNotificationStatusQuery(
    Guid NotificationId,
    string? TenantId,
    bool IsAdmin
) : IQuery<Result<NotificationStatus>>;
