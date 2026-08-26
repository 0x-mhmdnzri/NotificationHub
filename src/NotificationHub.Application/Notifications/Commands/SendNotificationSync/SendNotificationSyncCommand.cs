using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Common.Interfaces;

namespace NotificationHub.Application.Notifications.Commands.SendNotificationSync;

public sealed record SendNotificationSyncCommand(
    NotificationRequest Request,
    string? ResolvedTenantId
) : ICommand<DeliveryResult>;
