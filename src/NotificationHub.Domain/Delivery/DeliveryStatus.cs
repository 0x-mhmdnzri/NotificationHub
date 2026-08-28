namespace NotificationHub.Domain.Delivery;

/// <summary>Lifecycle of a single notification delivery (separate from broadcast campaign status).</summary>
public enum DeliveryStatus
{
    Queued = 0,
    Processing = 1,
    Sent = 2,
    Delivered = 3,
    Failed = 4,
    DeadLetter = 5,
    Scheduled = 6,
    Cancelled = 7,
    Collapsed = 8,
    Suppressed = 9
}
