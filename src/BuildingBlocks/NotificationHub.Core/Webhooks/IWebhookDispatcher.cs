namespace NotificationHub.Core.Webhooks;

public interface IWebhookDispatcher
{
    Task DispatchAsync(string eventName, object payload, string? tenantId = null, CancellationToken ct = default);
}
