using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Persistence;

public sealed class NotificationStatusEntity
{
    public Guid Id { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public DeliveryStatus Status { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public string? TenantId { get; set; }
    public string? IdempotencyKey { get; set; }
    public string? CorrelationId { get; set; }

    public static NotificationStatusEntity FromModel(NotificationStatus model) => new()
    {
        Id = model.NotificationId,
        Channel = model.Channel,
        Recipient = model.Recipient,
        Status = model.Status,
        ProviderMessageId = model.ProviderMessageId,
        ErrorCode = model.ErrorCode,
        ErrorMessage = model.ErrorMessage,
        AttemptCount = model.AttemptCount,
        CreatedAt = model.CreatedAt,
        UpdatedAt = model.UpdatedAt,
        TenantId = model.TenantId,
        IdempotencyKey = model.IdempotencyKey,
        CorrelationId = model.CorrelationId
    };

    public NotificationStatus ToModel() => new()
    {
        NotificationId = Id,
        Channel = Channel,
        Recipient = Recipient,
        Status = Status,
        ProviderMessageId = ProviderMessageId,
        ErrorCode = ErrorCode,
        ErrorMessage = ErrorMessage,
        AttemptCount = AttemptCount,
        CreatedAt = CreatedAt,
        UpdatedAt = UpdatedAt,
        TenantId = TenantId,
        IdempotencyKey = IdempotencyKey,
        CorrelationId = CorrelationId
    };
}
