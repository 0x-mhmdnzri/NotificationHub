using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Inbox;

public interface IInboxFeedService
{
    Task<InboxFeedResponse> GetFeedAsync(string userId, string? tenantId, bool includeArchived = false, int take = 50, CancellationToken ct = default);
    Task<InboxItem> PushAsync(InboxItem item, CancellationToken ct = default);
    Task<bool> MarkReadAsync(Guid id, string userId, string? tenantId, CancellationToken ct = default);
    Task<int> MarkAllReadAsync(string userId, string? tenantId, CancellationToken ct = default);
    Task<bool> ArchiveAsync(Guid id, string userId, string? tenantId, CancellationToken ct = default);
    IAsyncEnumerable<InboxItem> StreamAsync(string userId, string? tenantId, CancellationToken ct = default);
}
