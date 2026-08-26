using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Notifications.SendSync;

public sealed record SendNotificationSyncCommand(
    NotificationRequest Request,
    string? TrustedTenantId
) : ICommand<Result<DeliveryResult>>;
