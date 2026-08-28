namespace NotificationHub.Core.Messaging;

public sealed class NullIntegrationEventPublisher : IIntegrationEventPublisher
{
    public Task PublishAsync(
        string eventType, int version, Guid messageId, string payloadJson,
        string? tenantId, string? correlationId, CancellationToken ct = default)
        => Task.CompletedTask;
}
