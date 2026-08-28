using System.Text.Json;
using NotificationHub.Core.Persistence;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Events;

namespace NotificationHub.Infrastructure.Messaging;

/// <summary>
/// Persists domain events as durable outbox rows (same transaction boundary as caller SaveChanges when shared DbContext).
/// OutboxRelay publishes integration payload to RabbitMQ without Domain knowing about the broker.
/// </summary>
public sealed class OutboxDomainEventDispatcher(NotificationDbContext db) : IDomainEventDispatcher
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public async Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        foreach (var e in events)
        {
            var envelope = new
            {
                messageId = e.EventId,
                eventType = e.GetType().Name,
                version = 1,
                occurredAt = e.OccurredAtUtc,
                payload = e
            };

            db.OutboxMessages.Add(new OutboxMessageEntity
            {
                Id = Guid.NewGuid(),
                // Domain/integration events are not delivery notifications; use EventId as correlation key.
                NotificationId = e.EventId,
                PayloadJson = JsonSerializer.Serialize(envelope, JsonOpts),
                Status = "pending",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        await db.SaveChangesAsync(ct);
    }
}
