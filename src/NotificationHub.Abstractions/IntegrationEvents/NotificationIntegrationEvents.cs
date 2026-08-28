namespace NotificationHub.Abstractions.IntegrationEvents;

// Versioned contracts for downstream systems. Breaking change => new V2 type.

public sealed record NotificationAcceptedV1(
    Guid NotificationId,
    string Recipient,
    string Channel,
    string TemplateKey,
    string? TenantId,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public string EventType => "notification.accepted";
    public int Version => 1;
}

public sealed record NotificationSuppressedV1(
    Guid NotificationId,
    string Recipient,
    string Channel,
    string Reason,
    string? TenantId,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public string EventType => "notification.suppressed";
    public int Version => 1;
}

public sealed record NotificationSentV1(
    Guid NotificationId,
    string ProviderId,
    string? ProviderMessageId,
    string? TenantId,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public string EventType => "notification.sent";
    public int Version => 1;
}

public sealed record NotificationFailedV1(
    Guid NotificationId,
    string? ErrorCode,
    string? ErrorMessage,
    int AttemptNumber,
    string? TenantId,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public string EventType => "notification.failed";
    public int Version => 1;
}

public sealed record NotificationDeadLetteredV1(
    Guid NotificationId,
    string? Reason,
    string? TenantId,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public string EventType => "notification.dead_lettered";
    public int Version => 1;
}

public sealed record NotificationCancelledV1(
    Guid NotificationId,
    string? TenantId,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public string EventType => "notification.cancelled";
    public int Version => 1;
}

public sealed record CampaignStatusChangedV1(
    Guid CampaignId,
    string FromStatus,
    string ToStatus,
    string? TenantId,
    DateTimeOffset OccurredAtUtc) : IIntegrationEvent
{
    public string EventType => "campaign.status_changed";
    public int Version => 1;
}
