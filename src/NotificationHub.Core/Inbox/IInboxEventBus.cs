using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Inbox;

public interface IInboxEventBus
{
    Task PublishAsync(InboxItem item, CancellationToken ct = default);
    IAsyncEnumerable<InboxItem> SubscribeAsync(string userId, string? tenantId, CancellationToken ct = default);
}
