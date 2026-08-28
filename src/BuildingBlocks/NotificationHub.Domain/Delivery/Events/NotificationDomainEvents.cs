using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery.ValueObjects;

namespace NotificationHub.Domain.Delivery.Events;

public sealed record NotificationAccepted(
    NotificationId NotificationId,
    RecipientAddress Recipient,
    ChannelCode Channel,
    TemplateKey TemplateKey,
    string? TenantId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record NotificationMarkedProcessing(
    NotificationId NotificationId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record NotificationSent(
    NotificationId NotificationId,
    string ProviderId,
    string? ProviderMessageId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record NotificationDeliveryFailed(
    NotificationId NotificationId,
    string? ErrorCode,
    string? ErrorMessage,
    int AttemptNumber,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record NotificationDeadLettered(
    NotificationId NotificationId,
    string? Reason,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record NotificationCancelled(
    NotificationId NotificationId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}

public sealed record NotificationSuppressed(
    NotificationId NotificationId,
    RecipientAddress Recipient,
    ChannelCode Channel,
    string Reason,
    string? TenantId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}
