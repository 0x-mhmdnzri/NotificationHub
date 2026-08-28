using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery.Events;
using NotificationHub.Domain.Delivery.ValueObjects;

namespace NotificationHub.Domain.Delivery;

/// <summary>
/// Aggregate root for a single notification delivery.
/// Consistency boundary: status transitions, attempt count, suppression rules for one delivery.
/// </summary>
public sealed class Notification : AggregateRoot<NotificationId>
{
    private static readonly HashSet<(DeliveryStatus From, DeliveryStatus To)> Allowed =
    [
        (DeliveryStatus.Queued, DeliveryStatus.Processing),
        (DeliveryStatus.Queued, DeliveryStatus.Cancelled),
        (DeliveryStatus.Queued, DeliveryStatus.Suppressed),
        (DeliveryStatus.Queued, DeliveryStatus.Collapsed),
        (DeliveryStatus.Scheduled, DeliveryStatus.Queued),
        (DeliveryStatus.Scheduled, DeliveryStatus.Cancelled),
        (DeliveryStatus.Scheduled, DeliveryStatus.Suppressed),
        (DeliveryStatus.Processing, DeliveryStatus.Sent),
        (DeliveryStatus.Processing, DeliveryStatus.Failed),
        (DeliveryStatus.Processing, DeliveryStatus.DeadLetter),
        (DeliveryStatus.Failed, DeliveryStatus.Processing), // retry
        (DeliveryStatus.Failed, DeliveryStatus.DeadLetter),
        (DeliveryStatus.Sent, DeliveryStatus.Delivered),
        (DeliveryStatus.Sent, DeliveryStatus.Failed)
    ];

    public RecipientAddress Recipient { get; private set; } = null!;
    public ChannelCode Channel { get; private set; } = null!;
    public TemplateKey TemplateKey { get; private set; } = null!;
    public DeliveryStatus Status { get; private set; }
    public NotificationPriority Priority { get; private set; }
    public IdempotencyKey? IdempotencyKey { get; private set; }
    public CollapseKey? CollapseKey { get; private set; }
    public TenantId? TenantId { get; private set; }
    public string? Locale { get; private set; }
    public string? Category { get; private set; }
    public string? CorrelationId { get; private set; }
    public string? PreferredProvider { get; private set; }
    public bool AllowFallback { get; private set; }
    public DateTimeOffset? ScheduledAtUtc { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }
    public string? ProviderId { get; private set; }
    public string? ProviderMessageId { get; private set; }

    /// <summary>Opaque template data bag — not part of consistency rules.</summary>
    public IReadOnlyDictionary<string, object?> Data { get; private set; } = new Dictionary<string, object?>();

    private Notification() { } // EF / rehydration

    public static Notification Accept(
        NotificationId id,
        RecipientAddress recipient,
        ChannelCode channel,
        TemplateKey templateKey,
        NotificationPriority priority,
        IdempotencyKey? idempotencyKey,
        CollapseKey? collapseKey,
        TenantId? tenantId,
        string? locale,
        string? category,
        string? correlationId,
        string? preferredProvider,
        bool allowFallback,
        DateTimeOffset? scheduledAtUtc,
        IReadOnlyDictionary<string, object?>? data,
        DateTimeOffset nowUtc)
    {
        var status = scheduledAtUtc is { } s && s > nowUtc
            ? DeliveryStatus.Scheduled
            : DeliveryStatus.Queued;

        var n = new Notification
        {
            Id = id,
            Recipient = recipient,
            Channel = channel,
            TemplateKey = templateKey,
            Status = status,
            Priority = priority,
            IdempotencyKey = idempotencyKey,
            CollapseKey = collapseKey,
            TenantId = tenantId,
            Locale = locale ?? "en",
            Category = category,
            CorrelationId = correlationId,
            PreferredProvider = preferredProvider,
            AllowFallback = allowFallback,
            ScheduledAtUtc = scheduledAtUtc,
            CreatedAtUtc = nowUtc,
            AttemptCount = 0,
            Data = data is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(data)
        };

        n.Raise(new NotificationAccepted(
            id, recipient, channel, templateKey, tenantId?.Value, nowUtc));
        return n;
    }

    /// <summary>Rehydrate from persistence without raising events.</summary>
    public static Notification Rehydrate(
        NotificationId id,
        RecipientAddress recipient,
        ChannelCode channel,
        TemplateKey templateKey,
        DeliveryStatus status,
        NotificationPriority priority,
        IdempotencyKey? idempotencyKey,
        CollapseKey? collapseKey,
        TenantId? tenantId,
        string? locale,
        string? category,
        string? correlationId,
        string? preferredProvider,
        bool allowFallback,
        DateTimeOffset? scheduledAtUtc,
        DateTimeOffset createdAtUtc,
        DateTimeOffset? processedAtUtc,
        int attemptCount,
        string? lastErrorCode,
        string? lastErrorMessage,
        string? providerId,
        string? providerMessageId,
        IReadOnlyDictionary<string, object?>? data)
    {
        return new Notification
        {
            Id = id,
            Recipient = recipient,
            Channel = channel,
            TemplateKey = templateKey,
            Status = status,
            Priority = priority,
            IdempotencyKey = idempotencyKey,
            CollapseKey = collapseKey,
            TenantId = tenantId,
            Locale = locale,
            Category = category,
            CorrelationId = correlationId,
            PreferredProvider = preferredProvider,
            AllowFallback = allowFallback,
            ScheduledAtUtc = scheduledAtUtc,
            CreatedAtUtc = createdAtUtc,
            ProcessedAtUtc = processedAtUtc,
            AttemptCount = attemptCount,
            LastErrorCode = lastErrorCode,
            LastErrorMessage = lastErrorMessage,
            ProviderId = providerId,
            ProviderMessageId = providerMessageId,
            Data = data is null
                ? new Dictionary<string, object?>()
                : new Dictionary<string, object?>(data)
        };
    }

    public void MarkProcessing(DateTimeOffset nowUtc)
    {
        EnsureTransition(DeliveryStatus.Processing);
        Status = DeliveryStatus.Processing;
        AttemptCount++;
        ProcessedAtUtc = nowUtc;
        Raise(new NotificationMarkedProcessing(Id, nowUtc));
    }

    public void MarkSent(string providerId, string? providerMessageId, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(providerId))
            throw new DomainException("Provider id is required when marking sent.");
        EnsureTransition(DeliveryStatus.Sent);
        Status = DeliveryStatus.Sent;
        ProviderId = providerId;
        ProviderMessageId = providerMessageId;
        ProcessedAtUtc = nowUtc;
        LastErrorCode = null;
        LastErrorMessage = null;
        Raise(new NotificationSent(Id, providerId, providerMessageId, nowUtc));
    }

    public void MarkDelivered(DateTimeOffset nowUtc)
    {
        EnsureTransition(DeliveryStatus.Delivered);
        Status = DeliveryStatus.Delivered;
        ProcessedAtUtc = nowUtc;
    }

    public void MarkFailed(string? errorCode, string? errorMessage, int maxAttempts, DateTimeOffset nowUtc)
    {
        EnsureTransition(DeliveryStatus.Failed);
        Status = DeliveryStatus.Failed;
        LastErrorCode = errorCode;
        LastErrorMessage = errorMessage;
        ProcessedAtUtc = nowUtc;
        Raise(new NotificationDeliveryFailed(Id, errorCode, errorMessage, AttemptCount, nowUtc));

        if (AttemptCount >= maxAttempts)
            DeadLetter(errorMessage ?? "Max attempts exceeded", nowUtc);
    }

    public void DeadLetter(string reason, DateTimeOffset nowUtc)
    {
        if (Status is DeliveryStatus.DeadLetter or DeliveryStatus.Cancelled or DeliveryStatus.Delivered)
            throw new DomainException($"Cannot dead-letter notification in status {Status}.");
        if (!Allowed.Contains((Status, DeliveryStatus.DeadLetter)) && Status != DeliveryStatus.Failed)
            throw new DomainException($"Illegal transition {Status} → DeadLetter.");
        Status = DeliveryStatus.DeadLetter;
        LastErrorMessage = reason;
        ProcessedAtUtc = nowUtc;
        Raise(new NotificationDeadLettered(Id, reason, nowUtc));
    }

    public void Cancel(DateTimeOffset nowUtc)
    {
        EnsureTransition(DeliveryStatus.Cancelled);
        Status = DeliveryStatus.Cancelled;
        ProcessedAtUtc = nowUtc;
        Raise(new NotificationCancelled(Id, nowUtc));
    }

    public void Suppress(string reason, DateTimeOffset nowUtc)
    {
        EnsureTransition(DeliveryStatus.Suppressed);
        Status = DeliveryStatus.Suppressed;
        LastErrorMessage = reason;
        ProcessedAtUtc = nowUtc;
        Raise(new NotificationSuppressed(Id, reason, nowUtc));
    }

    public void Collapse(DateTimeOffset nowUtc)
    {
        EnsureTransition(DeliveryStatus.Collapsed);
        Status = DeliveryStatus.Collapsed;
        ProcessedAtUtc = nowUtc;
    }

    /// <summary>Promote scheduled notification when due.</summary>
    public void ReleaseSchedule(DateTimeOffset nowUtc)
    {
        if (Status != DeliveryStatus.Scheduled)
            throw new DomainException("Only scheduled notifications can be released.");
        if (ScheduledAtUtc is { } s && s > nowUtc)
            throw new DomainException("Notification is not due yet.");
        EnsureTransition(DeliveryStatus.Queued);
        Status = DeliveryStatus.Queued;
    }

    public bool IsTerminal => Status is DeliveryStatus.Delivered
        or DeliveryStatus.DeadLetter
        or DeliveryStatus.Cancelled
        or DeliveryStatus.Collapsed
        or DeliveryStatus.Suppressed;

    public bool CanRetry(int maxAttempts) =>
        Status == DeliveryStatus.Failed && AttemptCount < maxAttempts;

    private void EnsureTransition(DeliveryStatus to)
    {
        if (Status == to) return;
        if (!Allowed.Contains((Status, to)))
            throw new DomainException($"Illegal notification transition {Status} → {to}.");
    }
}
