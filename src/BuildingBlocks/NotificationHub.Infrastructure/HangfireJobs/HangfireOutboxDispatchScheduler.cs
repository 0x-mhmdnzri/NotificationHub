using Hangfire;
using Hangfire.States;
using NotificationHub.Core.Messaging;

namespace NotificationHub.Infrastructure.HangfireJobs;

/// <summary>
/// Enqueues Hangfire jobs by OutboxMessage.Id onto isolated queues (critical / notifications / outbox).
/// </summary>
public sealed class HangfireOutboxDispatchScheduler : IOutboxDispatchScheduler
{
    public void ScheduleDispatch(Guid outboxMessageId, string? queue = null)
    {
        var q = string.IsNullOrWhiteSpace(queue) ? MessagingQueues.Notifications : queue.Trim().ToLowerInvariant();
        var client = new BackgroundJobClient();
        client.Create<IOutboxDispatchJob>(
            j => j.DispatchAsync(outboxMessageId, CancellationToken.None),
            new EnqueuedState(q));
    }

    public void ScheduleDispatchBatch(IReadOnlyList<Guid> outboxMessageIds, string? queue = null)
    {
        foreach (var id in outboxMessageIds)
            ScheduleDispatch(id, queue);
    }
}
