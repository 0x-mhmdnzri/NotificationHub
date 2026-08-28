namespace NotificationHub.Infrastructure.HangfireJobs;

public interface IOutboxDispatchJob
{
    Task DispatchAsync(Guid outboxMessageId, CancellationToken cancellationToken);
}
