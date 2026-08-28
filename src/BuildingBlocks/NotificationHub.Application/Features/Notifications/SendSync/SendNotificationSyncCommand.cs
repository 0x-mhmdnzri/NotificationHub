using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Notifications.SendSync;

[AuthorizeRoles(AppRoles.Admin, AppRoles.Sender)]
public sealed record SendNotificationSyncCommand(
    NotificationRequest Request,
    string? TrustedTenantId
) : ICommand<Result<DeliveryResult>>;
