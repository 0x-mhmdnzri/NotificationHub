namespace NotificationHub.Core.Messaging;

/// <summary>
/// Publishes versioned integration events to the platform events exchange (not delivery work queues).
/// </summary>
public interface IIntegrationEventPublisher
{
    Task PublishAsync(
        string eventType,
        int version,
        Guid messageId,
        string payloadJson,
        string? tenantId,
        string? correlationId,
        CancellationToken ct = default);
}
