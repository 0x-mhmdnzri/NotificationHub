namespace NotificationHub.Core.Messaging;

/// <summary>
/// Schedules durable dispatch of a committed outbox row.
/// Implementation: Hangfire job by outbox id (not the full payload).
/// </summary>
public interface IOutboxDispatchScheduler
{
    void ScheduleDispatch(Guid outboxMessageId, string? queue = null);
    void ScheduleDispatchBatch(IReadOnlyList<Guid> outboxMessageIds, string? queue = null);
}
