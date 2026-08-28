using NotificationHub.Domain.Common;

namespace NotificationHub.Domain.Events;

/// <summary>
/// Collects domain events from aggregates after successful persistence and
/// maps them to durable integration work (outbox). Domain stays free of brokers.
/// </summary>
public interface IDomainEventDispatcher
{
    Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default);
}
