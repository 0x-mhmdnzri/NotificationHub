namespace NotificationHub.Abstractions.Models;

public sealed record NotificationRequest
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Recipient { get; init; }
    public required string Channel { get; init; }
    public required string TemplateKey { get; init; }
    public Dictionary<string, object?> Data { get; init; } = new();
    public NotificationPriority Priority { get; init; } = NotificationPriority.Normal;
    public DateTimeOffset? ScheduledAt { get; init; }
    public string? IdempotencyKey { get; init; }
}

public enum NotificationPriority
{
    Low = 0,
    Normal = 1,
    High = 2,
    Critical = 3
}

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
}

public sealed record NotificationAttachment(string FileName, string ContentType, byte[] Content);

public sealed record DeliveryResult
{
    public required bool Success { get; init; }
    public string? ProviderMessageId { get; init; }
    public string? ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset AttemptedAt { get; init; } = DateTimeOffset.UtcNow;
}
