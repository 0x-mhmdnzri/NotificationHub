using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Chat.Slack;

public sealed class SlackPlugin : IChannelPlugin
{
    private string? _webhookUrl;
    private HttpClient? _http;
    private ILogger? _logger;

    public string Id => "chat-slack";
    public Version Version => new(1, 0, 0);
    public string Name => "Slack Incoming Webhook";
    public string Channel => "slack";
    public PluginCapability[] Capabilities => [new("channel", "slack")];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.Logger;
        _webhookUrl = context.Configuration["Plugins:Slack:WebhookUrl"];
        if (!string.IsNullOrWhiteSpace(_webhookUrl) && !IsSafeWebhook(_webhookUrl))
        {
            _logger?.LogWarning("Slack webhook URL rejected by safety checks; plugin disabled");
            _webhookUrl = null;
        }

        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(!string.IsNullOrWhiteSpace(_webhookUrl),
            string.IsNullOrWhiteSpace(_webhookUrl) ? "Missing or unsafe webhook" : "OK"));

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null || string.IsNullOrWhiteSpace(_webhookUrl))
            return new DeliveryResult { Success = false, ErrorCode = "slack_not_configured", ErrorMessage = "Slack webhook not configured" };

        try
        {
            var payload = new { text = string.IsNullOrWhiteSpace(notification.Subject) ? notification.Body : $"*{notification.Subject}*\n{notification.Body}" };
            using var resp = await _http.PostAsJsonAsync(_webhookUrl, payload, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                return new DeliveryResult
                {
                    Success = false,
                    ProviderId = Id,
                    ErrorCode = $"http_{(int)resp.StatusCode}",
                    ErrorMessage = body.Length > 200 ? body[..200] : body
                };
            }

            return new DeliveryResult { Success = true, ProviderId = Id, ProviderMessageId = Guid.NewGuid().ToString("N") };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Slack send failed");
            return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = "slack_error", ErrorMessage = ex.Message };
        }
    }

    /// <summary>SEC-29: only https public hosts (no loopback / private IP literals).</summary>
    public static bool IsSafeWebhook(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) return false;
        if (uri.IsLoopback) return false;
        if (System.Net.IPAddress.TryParse(uri.Host, out var ip))
        {
            if (System.Net.IPAddress.IsLoopback(ip)) return false;
            var bytes = ip.GetAddressBytes();
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                if (bytes[0] == 10) return false;
                if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
                if (bytes[0] == 192 && bytes[1] == 168) return false;
                if (bytes[0] == 169 && bytes[1] == 254) return false;
                if (bytes[0] == 127) return false;
            }
        }
        return true;
    }
}
