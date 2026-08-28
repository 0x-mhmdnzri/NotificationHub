using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery.ValueObjects;

namespace NotificationHub.Domain.Templates.Events;

public sealed record TemplateSaved(
    TemplateId TemplateId,
    TemplateKey Key,
    string? TenantId,
    int Version,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}
