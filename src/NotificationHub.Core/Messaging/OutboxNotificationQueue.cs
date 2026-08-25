using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Queue;

namespace NotificationHub.Core.Messaging;

/// <summary>
/// API-facing queue: writes to outbox only. Relay publishes to RabbitMQ.
/// </summary>
public sealed class OutboxNotificationQueue : INotificationQueue
{
    private readonly IOutbox _outbox;
    public OutboxNotificationQueue(IOutbox outbox) => _outbox = outbox;

    public ValueTask EnqueueAsync(NotificationRequest request, CancellationToken ct = default)
        => new(_outbox.AddAsync(request, ct));

    public async IAsyncEnumerable<NotificationRequest> DequeueAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Consumption is handled by NotificationBackgroundWorker via RabbitMqNotificationQueue
        await Task.Yield();
        yield break;
    }
}
