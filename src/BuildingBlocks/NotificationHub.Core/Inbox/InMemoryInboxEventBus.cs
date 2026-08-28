using System.Runtime.CompilerServices;
using System.Threading.Channels;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Inbox;

public sealed class InMemoryInboxEventBus : IInboxEventBus
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Channel<InboxItem>> Streams = new();

    public Task PublishAsync(InboxItem item, CancellationToken ct = default)
    {
        var key = Key(item.UserId, item.TenantId);
        // GetOrAdd so a publish that races ahead of Subscribe still buffers the event
        var ch = Streams.GetOrAdd(key, _ => Channel.CreateBounded<InboxItem>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        }));
        ch.Writer.TryWrite(item);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<InboxItem> SubscribeAsync(string userId, string? tenantId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = Key(userId, tenantId);
        var ch = Streams.GetOrAdd(key, _ => Channel.CreateBounded<InboxItem>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        }));
        await foreach (var item in ch.Reader.ReadAllAsync(ct))
            yield return item;
    }

    private static string Key(string userId, string? tenantId) => $"{tenantId ?? "_"}:{userId}";
}
