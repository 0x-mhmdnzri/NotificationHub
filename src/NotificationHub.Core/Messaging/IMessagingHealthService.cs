namespace NotificationHub.Core.Messaging;

public interface IMessagingHealthService
{
    Task<MessagingHealthSnapshot> CheckAsync(CancellationToken ct = default);
}
