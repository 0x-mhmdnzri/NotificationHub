using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Channels;
using NotificationHub.Abstractions.Models;
using StackExchange.Redis;

namespace NotificationHub.Core.Inbox;

/// <summary>F32 — Redis pub/sub for multi-instance inbox SSE.</summary>
public sealed class RedisInboxEventBus : IInboxEventBus
{
    private readonly IConnectionMultiplexer _mux;
    private const string ChannelPrefix = "nh:inbox:";

    public RedisInboxEventBus(IConnectionMultiplexer mux) => _mux = mux;

    public async Task PublishAsync(InboxItem item, CancellationToken ct = default)
    {
        var sub = _mux.GetSubscriber();
        var payload = JsonSerializer.Serialize(item);
        await sub.PublishAsync(RedisChannel.Literal(ChannelPrefix + Key(item.UserId, item.TenantId)), payload);
    }

    public async IAsyncEnumerable<InboxItem> SubscribeAsync(string userId, string? tenantId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var sub = _mux.GetSubscriber();
        var channel = System.Threading.Channels.Channel.CreateUnbounded<InboxItem>();
        await sub.SubscribeAsync(RedisChannel.Literal(ChannelPrefix + Key(userId, tenantId)), (_, value) =>
        {
            try
            {
                var item = JsonSerializer.Deserialize<InboxItem>((string)value!);
                if (item is not null)
                    channel.Writer.TryWrite(item);
            }
            catch { /* ignore bad payload */ }
        });

        try
        {
            await foreach (var item in channel.Reader.ReadAllAsync(ct))
                yield return item;
        }
        finally
        {
            await sub.UnsubscribeAsync(RedisChannel.Literal(ChannelPrefix + Key(userId, tenantId)));
            channel.Writer.TryComplete();
        }
    }

    private static string Key(string userId, string? tenantId) => $"{tenantId ?? "_"}:{userId}";
}
