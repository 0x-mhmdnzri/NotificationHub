using System.Text.Json;
using NotificationHub.Core.Persistence;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Events;
using NotificationHub.Infrastructure.Messaging.Integration;

namespace NotificationHub.Infrastructure.Messaging;

/// <summary>
/// Stages **integration** events (mapped from domain events) into the outbox.
/// Domain events themselves never leave the process boundary.
/// Payload kind = "integration" so Hangfire dispatch can route differently from delivery outbox.
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
        foreach (var domainEvent in events)
        {
            var integration = DomainEventToIntegrationMapper.TryMap(domainEvent);
            if (integration is null)
                continue;

            var wire = new
            {
                kind = "integration",
                messageId = integration.MessageId,
                eventType = integration.EventType,
                version = integration.Version,
                occurredAt = integration.OccurredAtUtc,
                correlationId = integration.CorrelationId,
                tenantId = integration.TenantId,
                payload = integration.Payload
            };

            db.OutboxMessages.Add(new OutboxMessageEntity
            {
                Id = Guid.NewGuid(),
                NotificationId = integration.MessageId,
                PayloadJson = JsonSerializer.Serialize(wire, JsonOpts),
                Status = "pending",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }

        return Task.CompletedTask;
    }
}
