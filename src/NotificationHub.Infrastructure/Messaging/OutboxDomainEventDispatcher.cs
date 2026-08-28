using System.Text.Json;
using NotificationHub.Core.Persistence;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Events;

namespace NotificationHub.Infrastructure.Messaging;

/// <summary>
/// Stages domain events as durable outbox rows. Commit with IUnitOfWork / shared DbContext.
/// </summary>
public sealed class OutboxDomainEventDispatcher(NotificationDbContext db) : IDomainEventDispatcher
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public Task DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
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
                NotificationId = e.EventId,
                PayloadJson = JsonSerializer.Serialize(envelope, JsonOpts),
                Status = "pending",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        return Task.CompletedTask;
    }
}
