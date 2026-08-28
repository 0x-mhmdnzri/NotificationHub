namespace NotificationHub.Abstractions.IntegrationEvents;

/// <summary>
/// Wire-level envelope for integration events. Domain events never leave the process as-is.
/// </summary>
public sealed record IntegrationEventEnvelope(
    Guid MessageId,
    string EventType,
    int Version,
    DateTimeOffset OccurredAtUtc,
    string? CorrelationId,
    string? TenantId,
    object Payload);

/// <summary>Marker for versioned integration payloads.</summary>
public interface IIntegrationEvent
{
    string EventType { get; }
    int Version { get; }
}
