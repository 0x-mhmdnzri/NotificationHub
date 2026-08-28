using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Engagement;

/// <summary>Records and queries engagement events (SRP).</summary>
public interface IEngagementService
{
    /// <param name="requireExistingNotification">
    /// When true (default), returns null and does not persist if notification is missing (SEC-21/22).
    /// </param>
    Task<EngagementEvent?> TrackAsync(EngagementEvent evt, bool requireExistingNotification = true, CancellationToken ct = default);

    Task<IReadOnlyList<EngagementEvent>> ListByNotificationAsync(Guid notificationId, CancellationToken ct = default);

    Task<(long Opens, long Clicks)> CountAsync(DateTimeOffset? from, DateTimeOffset? to, string? tenantId, CancellationToken ct = default);
}
