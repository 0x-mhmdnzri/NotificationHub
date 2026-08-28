namespace NotificationHub.Core.Messaging;

public sealed class NullOutboxDispatchScheduler : IOutboxDispatchScheduler
{
    public void ScheduleDispatch(Guid outboxMessageId, string? queue = null) { }
    public void ScheduleDispatchBatch(IReadOnlyList<Guid> outboxMessageIds, string? queue = null) { }
}
