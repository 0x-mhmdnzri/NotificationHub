using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.PluginHost;

namespace NotificationHub.Core.Orchestration;

public sealed class NotificationOrchestrator
{
    private readonly PluginLoader _pluginLoader;
    private readonly ILogger<NotificationOrchestrator> _logger;

    public NotificationOrchestrator(PluginLoader pluginLoader, ILogger<NotificationOrchestrator> logger)
    {
        _pluginLoader = pluginLoader;
        _logger = logger;
    }

    public async Task<DeliveryResult> SendAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var channelPlugins = _pluginLoader.LoadedPlugins
            .OfType<IChannelPlugin>()
            .Where(p => p.Channel.Equals(request.Channel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (channelPlugins.Count == 0)
        {
            _logger.LogWarning("No plugin found for channel {Channel}", request.Channel);
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "NO_PLUGIN",
                ErrorMessage = $"No plugin registered for channel '{request.Channel}'"
            };
        }

        var plugin = channelPlugins[0];

        var rendered = new RenderedNotification
        {
            NotificationId = request.Id,
            Recipient = request.Recipient,
            Channel = request.Channel,
            Subject = request.TemplateKey,
            Body = $"Notification: {request.TemplateKey} | Data: {string.Join(", ", request.Data.Select(kv => $"{kv.Key}={kv.Value}"))}"
        };

        _logger.LogInformation("Sending notification {Id} via {Plugin} to {Recipient}", request.Id, plugin.Id, request.Recipient);

        try
        {
            return await plugin.SendAsync(rendered, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin {Plugin} failed to send notification {Id}", plugin.Id, request.Id);
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "PLUGIN_EXCEPTION",
                ErrorMessage = ex.Message
            };
        }
    }
}
