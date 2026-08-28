using NotificationHub.Abstractions.IntegrationEvents;
using NotificationHub.Domain.Broadcast.Events;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery.Events;

namespace NotificationHub.Infrastructure.Messaging.Integration;

/// <summary>
/// Maps internal Domain Events to stable Integration Event contracts.
/// Domain VOs are flattened to primitives so consumers never depend on Domain assembly.
/// </summary>
public static class DomainEventToIntegrationMapper
{
    public static IntegrationEventEnvelope? TryMap(IDomainEvent domainEvent, string? correlationId = null)
    {
        return domainEvent switch
        {
            NotificationAccepted e => Envelope(e.EventId, e.OccurredAtUtc, correlationId, e.TenantId,
                new NotificationAcceptedV1(e.NotificationId.Value, e.Recipient.Value, e.Channel.Value,
                    e.TemplateKey.Value, e.TenantId, e.OccurredAtUtc)),

            NotificationSuppressed e => Envelope(e.EventId, e.OccurredAtUtc, correlationId, e.TenantId,
                new NotificationSuppressedV1(e.NotificationId.Value, e.Recipient.Value, e.Channel.Value,
                    e.Reason, e.TenantId, e.OccurredAtUtc)),

            NotificationSent e => Envelope(e.EventId, e.OccurredAtUtc, correlationId, null,
                new NotificationSentV1(e.NotificationId.Value, e.ProviderId, e.ProviderMessageId, null, e.OccurredAtUtc)),

            NotificationDeliveryFailed e => Envelope(e.EventId, e.OccurredAtUtc, correlationId, null,
                new NotificationFailedV1(e.NotificationId.Value, e.ErrorCode, e.ErrorMessage, e.AttemptNumber, null, e.OccurredAtUtc)),

            NotificationDeadLettered e => Envelope(e.EventId, e.OccurredAtUtc, correlationId, null,
                new NotificationDeadLetteredV1(e.NotificationId.Value, e.Reason, null, e.OccurredAtUtc)),

            NotificationCancelled e => Envelope(e.EventId, e.OccurredAtUtc, correlationId, null,
                new NotificationCancelledV1(e.NotificationId.Value, null, e.OccurredAtUtc)),

            CampaignScheduled e => Envelope(e.EventId, e.OccurredAtUtc, correlationId, null,
                new CampaignStatusChangedV1(e.CampaignId.Value, "Draft", "Scheduled", null, e.OccurredAtUtc)),

            CampaignStarted e => Envelope(e.EventId, e.OccurredAtUtc, correlationId, null,
                new CampaignStatusChangedV1(e.CampaignId.Value, "Scheduled|Draft", "Processing", null, e.OccurredAtUtc)),

            CampaignCompleted e => Envelope(e.EventId, e.OccurredAtUtc, correlationId, null,
                new CampaignStatusChangedV1(e.CampaignId.Value, "Processing", e.FinalStatus.ToString(), null, e.OccurredAtUtc)),

            CampaignCancelled e => Envelope(e.EventId, e.OccurredAtUtc, correlationId, null,
                new CampaignStatusChangedV1(e.CampaignId.Value, "Any", "Cancelled", null, e.OccurredAtUtc)),

            // Internal process signals — not published as integration events
            NotificationMarkedProcessing => null,
            _ => null
        };
    }

    static IntegrationEventEnvelope Envelope(
        Guid messageId, DateTimeOffset occurredAt, string? correlationId, string? tenantId, IIntegrationEvent payload)
        => new(messageId, payload.EventType, payload.Version, occurredAt, correlationId, tenantId, payload);
}
