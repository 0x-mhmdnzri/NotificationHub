namespace NotificationHub.Core.Messaging;

/// <summary>
/// Isolated Hangfire / logical work queues (skill: do not put unrelated workloads on one queue).
/// </summary>
public static class MessagingQueues
{
    /// <summary>Tier-0 / critical notifications (OTP, security, payment alerts).</summary>
    public const string Critical = "critical";

    /// <summary>Standard notification delivery dispatch.</summary>
    public const string Notifications = "notifications";

    /// <summary>Generic outbox / integration events.</summary>
    public const string Outbox = "outbox";

    /// <summary>Default catch-all (maintenance, reconciliation).</summary>
    public const string Default = "default";

    public static string ForPriority(NotificationHub.Abstractions.Models.NotificationPriority priority)
        => priority == NotificationHub.Abstractions.Models.NotificationPriority.Critical
            ? Critical
            : Notifications;

    public static string ForDomainPriority(NotificationHub.Domain.Delivery.NotificationPriority priority)
        => priority == NotificationHub.Domain.Delivery.NotificationPriority.Critical
            ? Critical
            : Notifications;
}
