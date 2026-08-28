using NotificationHub.Core.Messaging;
using NotificationHub.Core.Queue;

namespace NotificationHub.Infrastructure.Messaging.Integration;

public sealed class RabbitMqIntegrationEventPublisher(RabbitMqNotificationQueue rabbit) : IIntegrationEventPublisher
{
    public Task PublishAsync(
        string eventType,
        int version,
        Guid messageId,
        string payloadJson,
        string? tenantId,
        string? correlationId,
        CancellationToken ct = default)
        => rabbit.PublishIntegrationEventAsync(eventType, version, messageId, payloadJson, tenantId, correlationId, ct);
}
