using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Engagement;

/// <summary>Records and queries engagement events (SRP).</summary>
public interface IEngagementService
{
    Task<EngagementEvent> TrackAsync(EngagementEvent evt, CancellationToken ct = default);
    Task<IReadOnlyList<EngagementEvent>> ListByNotificationAsync(Guid notificationId, CancellationToken ct = default);
    Task<(long Opens, long Clicks)> CountAsync(DateTimeOffset? from, DateTimeOffset? to, string? tenantId, CancellationToken ct = default);
}
