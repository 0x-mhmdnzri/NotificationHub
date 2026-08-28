namespace NotificationHub.Core.Messaging;

public sealed class NullOutboxDispatchScheduler : IOutboxDispatchScheduler
{
    public void ScheduleDispatch(Guid outboxMessageId) { }
    public void ScheduleDispatchBatch(IReadOnlyList<Guid> outboxMessageIds) { }
}
