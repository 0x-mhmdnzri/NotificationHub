using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Queue;

namespace NotificationHub.Core.Messaging;

/// <summary>
/// API-facing queue: writes to outbox only. Relay / Hangfire publishes to RabbitMQ.
/// </summary>
public sealed class OutboxNotificationQueue : INotificationQueue
{
    private readonly IOutbox _outbox;
    private readonly IOutboxDispatchScheduler _scheduler;
    public OutboxNotificationQueue(IOutbox outbox, IOutboxDispatchScheduler scheduler)
    {
        _outbox = outbox;
        _scheduler = scheduler;
    }

    public async ValueTask EnqueueAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var id = await _outbox.AddAsync(request, ct);
        // Note: caller must SaveChanges before this is reliable; Prefer orchestrator path.
        _scheduler.ScheduleDispatch(id);
    }

    public async IAsyncEnumerable<NotificationRequest> DequeueAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield break;
    }
}
