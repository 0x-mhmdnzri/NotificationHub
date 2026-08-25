namespace NotificationHub.Core.Sync;

public interface ICrossChannelReadSync
{
    /// <summary>F13 — when user opens/reads on any channel, mark related in-app items read.</summary>
    Task<int> SyncReadAsync(Guid notificationId, string? userId, string? tenantId, CancellationToken ct = default);
}
