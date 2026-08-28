namespace NotificationHub.Domain.Broadcast;

/// <summary>Broadcast lifecycle (orchestrated). Delivery outcomes live on recipients / notifications.</summary>
public enum CampaignStatus
{
    Draft = 0,
    Scheduled = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5,
    Preparing = 6,
    Dispatching = 7,
    Delivering = 8,
    PartiallyCompleted = 9
}
