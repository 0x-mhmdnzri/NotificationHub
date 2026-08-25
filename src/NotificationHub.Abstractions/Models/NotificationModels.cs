namespace NotificationHub.Abstractions.Models;

public sealed record NotificationRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Recipient { get; init; }
    public string? Channel { get; init; }
    public string[]? Channels { get; init; }
    public required string TemplateKey { get; init; }
    public Dictionary<string, object?> Data { get; init; } = new();
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;
    public DateTimeOffset? ScheduledAt { get; init; }
    public string? TimeZoneId { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? TenantId { get; init; }
    public string? Locale { get; init; } = "en";
    public string? CorrelationId { get; init; }
    public string? Category { get; init; }
    public string? PreferredProvider { get; init; }
    public bool AllowFallback { get; init; } = true;
    public IReadOnlyList<NotificationAttachment>? Attachments { get; init; }
}

public enum NotificationPriority { Low = 0, Normal = 1, High = 2, Critical = 3 }

public sealed record RenderedNotification
{
    public required Guid NotificationId { get; init; }
    public required string Recipient { get; init; }
    public required string Channel { get; init; }
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string? HtmlBody { get; init; }
    public Dictionary<string, string> Headers { get; init; } = new();
    public IReadOnlyList<NotificationAttachment>? Attachments { get; init; }
    public string? TenantId { get; init; }
    public string? Locale { get; init; }
    public string? PreferredProvider { get; init; }
}

public sealed record NotificationAttachment(string FileName, string ContentType, byte[] Content);

public sealed record DeliveryResult
{
    public required bool Success { get; init; }
    public string? ProviderId { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset AttemptedAt { get; init; } = DateTimeOffset.UtcNow;
    public int AttemptNumber { get; init; } = 1;
}

public enum DeliveryStatus
{
    Queued = 0, Processing = 1, Sent = 2, Delivered = 3,
    Failed = 4, DeadLetter = 5, Scheduled = 6, Cancelled = 7, Suppressed = 8
}

public sealed record NotificationStatus
{
    public required Guid NotificationId { get; init; }
    public required string Channel { get; init; }
    public required string Recipient { get; init; }
    public required DeliveryStatus Status { get; init; }
    public string? ProviderId { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public int AttemptCount { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ScheduledAt { get; init; }
    public string? TenantId { get; init; }
    public string? IdempotencyKey { get; init; }
    public string? CorrelationId { get; init; }
    public string? Category { get; init; }
}

public sealed record TemplateDefinition
{
    public required string Key { get; init; }
    public required string Channel { get; init; }
    public required string Locale { get; init; } = "en";
    public required string Subject { get; init; }
    public required string Body { get; init; }
    public string? HtmlBody { get; init; }
    public int Version { get; init; } = 1;
    public bool IsActive { get; init; } = true;
    public string? TenantId { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record UserPreference
{
    public required string UserId { get; init; }
    public string? TenantId { get; init; }
    public Dictionary<string, bool> ChannelOptIn { get; init; } = new();
    public Dictionary<string, bool> CategoryOptIn { get; init; } = new();
    public string? PreferredChannel { get; init; }
    public string? QuietHoursStart { get; init; }
    public string? QuietHoursEnd { get; init; }
    public string? TimeZoneId { get; init; }
    public int? MaxPerDay { get; init; }
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record AuditEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Action { get; init; }
    public Guid? NotificationId { get; init; }
    public string? TenantId { get; init; }
    public string? Actor { get; init; }
    public string? Details { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed record WebhookSubscription
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Url { get; init; }
    public string? Secret { get; init; }
    public string[] Events { get; init; } = ["sent", "failed", "delivered"];
    public string? TenantId { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record ProviderOptions
{
    public string? PreferredEmailProvider { get; set; } = "email-sendgrid";
    public string? PreferredSmsProvider { get; set; } = "sms-kavenegar";
    public string[] EmailFallbackOrder { get; set; } = ["email-sendgrid", "email-smtp"];
    public string[] SmsFallbackOrder { get; set; } = ["sms-kavenegar", "sms-smsir"];
}
