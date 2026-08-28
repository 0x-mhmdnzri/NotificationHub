using System.Text.Json;
using NotificationHub.Core.Persistence;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Events;
using NotificationHub.Infrastructure.Messaging.Integration;

namespace NotificationHub.Infrastructure.Messaging;

public sealed class OutboxDomainEventDispatcher(NotificationDbContext db) : IDomainEventDispatcher
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public Task<IReadOnlyList<Guid>> DispatchAsync(IEnumerable<IDomainEvent> events, CancellationToken ct = default)
    {
        var ids = new List<Guid>();
        foreach (var domainEvent in events)
        {
            var integration = DomainEventToIntegrationMapper.TryMap(domainEvent);
            if (integration is null)
                continue;

            var id = Guid.NewGuid();
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
                Id = id,
                NotificationId = integration.MessageId,
                PayloadJson = JsonSerializer.Serialize(wire, JsonOpts),
                Status = "pending",
                CreatedAt = DateTimeOffset.UtcNow
            });
            ids.Add(id);
        }

        return Task.FromResult<IReadOnlyList<Guid>>(ids);
    }
}
