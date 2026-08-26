namespace NotificationHub.Abstractions.Models;

public enum CampaignStatus
{
    Draft = 0,
    Scheduled = 1,
    Processing = 2,
    Completed = 3,
    Failed = 4,
    Cancelled = 5
}

public enum BroadcastRecipientStatus
{
    Pending = 0,
    Processing = 1,
    Queued = 2,
    Sent = 3,
    Failed = 4,
    DeadLettered = 5,
    Cancelled = 6,
    Skipped = 7
}

public sealed record BroadcastCampaign
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? TenantId { get; init; }
    public CampaignStatus Status { get; init; } = CampaignStatus.Draft;
    public required string TemplateKey { get; init; }
    /// <summary>Channels selected for ad delivery (sms, email, ...).</summary>
    public required string[] Channels { get; init; }
    public Dictionary<string, string>? Data { get; init; }
    public DateTimeOffset? ScheduledAtUtc { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? StartedAtUtc { get; init; }
    public DateTimeOffset? CompletedAtUtc { get; init; }
    public string? CreatedBy { get; init; }
}

public sealed record BroadcastRecipient
{
    public Guid Id { get; init; }
    public Guid CampaignId { get; init; }
    public required string Address { get; init; }
    public required string Channel { get; init; }
    public BroadcastRecipientStatus Status { get; init; }
    public int Attempts { get; init; }
    public Guid? NotificationId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? ProcessedAtUtc { get; init; }
}

public sealed record CampaignProgress
{
    public Guid CampaignId { get; init; }
    public CampaignStatus Status { get; init; }
    public long Total { get; init; }
    public long Pending { get; init; }
    public long Processing { get; init; }
    public long Queued { get; init; }
    public long Sent { get; init; }
    public long Failed { get; init; }
    public long DeadLettered { get; init; }
    public long Cancelled { get; init; }
    public long Skipped { get; init; }
}

// Backward-compatible simple request (maps to create+add+start)
public sealed record BroadcastRequest
{
    public required string Name { get; init; }
    public required string TemplateKey { get; init; }
    public string? Channel { get; init; }
    public string[]? Channels { get; init; }
    public List<string>? Recipients { get; init; }
    public Dictionary<string, string>? Data { get; init; }
    public string? TenantId { get; init; }
    public string? SegmentKey { get; init; }
    public string? Locale { get; init; }
}

public sealed record BroadcastResult
{
    public Guid CampaignId { get; init; }
    public int Accepted { get; init; }
    public int Failed { get; init; }
    public string Status { get; init; } = "queued";
}

public sealed record CreateCampaignRequest
{
    public required string Name { get; init; }
    public required string TemplateKey { get; init; }
    public required string[] Channels { get; init; }
    public Dictionary<string, string>? Data { get; init; }
    public DateTimeOffset? ScheduledAtUtc { get; init; }
    public string? TenantId { get; init; }
}

public sealed record AddRecipientsRequest
{
    public required List<string> Addresses { get; init; }
    /// <summary>Optional override; otherwise campaign channels are used (cartesian product).</summary>
    public string[]? Channels { get; init; }
}
