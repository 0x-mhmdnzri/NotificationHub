namespace NotificationHub.Abstractions.Models;

// F01 — Inbox feed
public sealed record InboxItem
{
    public Guid Id { get; init; }
    public string UserId { get; init; } = "";
    public string? TenantId { get; init; }
    public string Title { get; init; } = "";
    public string Body { get; init; } = "";
    public bool IsRead { get; init; }
    public bool IsArchived { get; init; }
    public Guid? NotificationId { get; init; }
    public string? Category { get; init; }
    public string? ActionUrl { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

public sealed record InboxFeedResponse
{
    public IReadOnlyList<InboxItem> Items { get; init; } = [];
    public int UnreadCount { get; init; }
    public DateTimeOffset? ServerTime { get; init; }
}

// F02 — Digest
public sealed record DigestPolicy
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Key { get; init; }
    public string? TenantId { get; init; }
    public int WindowMinutes { get; init; } = 60;
    public string Channel { get; init; } = "email";
    public string TemplateKey { get; init; } = "digest-default";
    public bool IsActive { get; init; } = true;
}

public sealed record DigestBufferEntry
{
    public Guid Id { get; init; }
    public string PolicyKey { get; init; } = "";
    public string Recipient { get; init; } = "";
    public string? TenantId { get; init; }
    public string PayloadJson { get; init; } = "{}";
    public DateTimeOffset CreatedAt { get; init; }
}

// F03 — Throttle
public sealed record ThrottlePolicy
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Key { get; init; }
    public string? TenantId { get; init; }
    public string? Channel { get; init; }
    public int MaxCount { get; init; } = 10;
    public int WindowMinutes { get; init; } = 60;
    public bool IsActive { get; init; } = true;
}

// F04 — Topics
public sealed record TopicDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Key { get; init; }
    public string? Name { get; init; }
    public string? TenantId { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record TopicSubscriber
{
    public Guid Id { get; init; }
    public string TopicKey { get; init; } = "";
    public string SubscriberId { get; init; } = "";
    public string? TenantId { get; init; }
    public string? Channel { get; init; }
    public string? Address { get; init; }
}

public sealed record TopicBroadcastRequest
{
    public required string TopicKey { get; init; }
    public required string TemplateKey { get; init; }
    public string? Channel { get; init; }
    public Dictionary<string, object?> Data { get; init; } = new();
    public string? TenantId { get; init; }
}

// F05 — Devices
public sealed record DeviceRegistration
{
    public Guid Id { get; init; }
    public required string UserId { get; init; }
    public string? TenantId { get; init; }
    public required string Platform { get; init; } // apns | fcm | webpush | expo
    public required string Token { get; init; }
    public string? Locale { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}

public sealed record RegisterDeviceRequest
{
    public required string UserId { get; init; }
    public string? TenantId { get; init; }
    public required string Platform { get; init; }
    public required string Token { get; init; }
    public string? Locale { get; init; }
}

// F06 — Activity
public sealed record ActivityItem
{
    public Guid Id { get; init; }
    public string Kind { get; init; } = ""; // notification | audit | engagement | workflow
    public string Summary { get; init; } = "";
    public Guid? NotificationId { get; init; }
    public string? TenantId { get; init; }
    public DateTimeOffset At { get; init; }
    public Dictionary<string, string?> Meta { get; init; } = new();
}
