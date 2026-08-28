using System.Threading.Channels;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Queue;

public sealed class InMemoryNotificationQueue : INotificationQueue
{
    private readonly Channel<NotificationRequest> _channel;

    public InMemoryNotificationQueue()
    {
        _channel = Channel.CreateUnbounded<NotificationRequest>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(NotificationRequest request, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(request, ct);

    public async IAsyncEnumerable<NotificationRequest> DequeueAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await foreach (var item in _channel.Reader.ReadAllAsync(ct))
        {
            yield return item;
        }
    }
}
