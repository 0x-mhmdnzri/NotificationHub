using Hangfire;
using NotificationHub.Core.Messaging;

namespace NotificationHub.Infrastructure.HangfireJobs;

/// <summary>
/// Enqueues Hangfire jobs that reference OutboxMessage.Id only (skill: prefer ID over huge DTO).
/// </summary>
public sealed class HangfireOutboxDispatchScheduler : IOutboxDispatchScheduler
{
    public void ScheduleDispatch(Guid outboxMessageId)
    {
        BackgroundJob.Enqueue<IOutboxDispatchJob>(
            j => j.DispatchAsync(outboxMessageId, CancellationToken.None));
    }

    public void ScheduleDispatchBatch(IReadOnlyList<Guid> outboxMessageIds)
    {
        foreach (var id in outboxMessageIds)
            ScheduleDispatch(id);
    }
}
