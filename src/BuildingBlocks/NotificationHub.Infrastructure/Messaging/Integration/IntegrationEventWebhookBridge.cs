using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotificationHub.Core.Webhooks;

namespace NotificationHub.Infrastructure.Messaging.Integration;

/// <summary>
/// Fires outbound webhooks from published integration events (not from domain events).
/// At-least-once: webhook handlers must be idempotent on messageId.
/// </summary>
public sealed class IntegrationEventWebhookBridge(
    IWebhookDispatcher webhooks,
    ILogger<IntegrationEventWebhookBridge> logger)
{
    private static readonly Dictionary<string, string> EventToWebhookName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["notification.accepted"] = "notification.accepted",
        ["notification.suppressed"] = "notification.suppressed",
        ["notification.sent"] = "notification.sent",
        ["notification.failed"] = "notification.failed",
        ["notification.dead_lettered"] = "notification.dead_lettered",
        ["notification.cancelled"] = "notification.cancelled",
        ["campaign.status_changed"] = "campaign.status_changed"
    };

    public async Task DispatchAsync(string eventType, string payloadJson, string? tenantId, CancellationToken ct)
    {
        if (!EventToWebhookName.TryGetValue(eventType, out var webhookEvent))
        {
            logger.LogDebug("No webhook mapping for integration event {EventType}", eventType);
            return;
        }

        object payload;
        try
        {
            payload = JsonSerializer.Deserialize<JsonElement>(payloadJson);
        }
        catch
        {
            payload = new { raw = payloadJson };
        }

        await webhooks.DispatchAsync(webhookEvent, payload, tenantId, ct);
    }
}
