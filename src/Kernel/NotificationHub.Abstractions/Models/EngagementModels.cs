namespace NotificationHub.Abstractions.Models;

public static class EngagementEventTypes
{
    public const string Open = "open";
    public const string Click = "click";
    public const string Unsubscribe = "unsubscribe";
    public const string Bounce = "bounce";
    public const string Delivered = "delivered";
}

public sealed record EngagementEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? NotificationId { get; init; }
    public string? TenantId { get; init; }
    public required string EventType { get; init; }
    public string? Recipient { get; init; }
    public string? Channel { get; init; }
    public string? Url { get; init; }
    public string? UserAgent { get; init; }
    public string? IpAddress { get; init; }
    public string? ProviderId { get; init; }
    public string? MetadataJson { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record EngagementIngestRequest
{
    public Guid? NotificationId { get; init; }
    public string? TenantId { get; init; }
    public required string EventType { get; init; }
    public string? Recipient { get; init; }
    public string? Channel { get; init; }
    public string? Url { get; init; }
    public string? ProviderId { get; init; }
    public Dictionary<string, string>? Metadata { get; init; }
}
