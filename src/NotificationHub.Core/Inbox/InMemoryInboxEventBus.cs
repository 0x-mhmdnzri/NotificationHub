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
        if (Streams.TryGetValue(key, out var ch))
            ch.Writer.TryWrite(item);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<InboxItem> SubscribeAsync(string userId, string? tenantId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = Key(userId, tenantId);
        var ch = Streams.GetOrAdd(key, _ => Channel.CreateBounded<InboxItem>(100));
        await foreach (var item in ch.Reader.ReadAllAsync(ct))
            yield return item;
    }

    private static string Key(string userId, string? tenantId) => $"{tenantId ?? "_"}:{userId}";
}
