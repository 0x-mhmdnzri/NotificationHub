using NotificationHub.Domain.Common;
using NotificationHub.Domain.Preferences.ValueObjects;

namespace NotificationHub.Domain.Preferences.Events;

public sealed record UserPreferenceUpdated(
    PreferenceId PreferenceId,
    UserId UserId,
    string? TenantId,
    DateTimeOffset OccurredAtUtc) : IDomainEvent
{
    public Guid EventId { get; } = Guid.NewGuid();
}
