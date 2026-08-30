
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Chat.Discord;

/// <summary>F16 — Discord incoming webhook.</summary>
public sealed class DiscordPlugin : IChannelPlugin
{
    private string? _webhookUrl;
    private HttpClient? _http;
    private ILogger? _logger;

    public string Id => "chat-discord";
    public Version Version => new(1, 0, 0);
    public string Name => "Discord Webhook";
    public string Channel => "discord";
    public PluginCapability[] Capabilities => [new("channel", "discord")];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.Logger;
        _webhookUrl = context.Configuration["Plugins:Discord:WebhookUrl"];
        if (!string.IsNullOrWhiteSpace(_webhookUrl) && !IsSafeHttps(_webhookUrl))
        {
            _logger?.LogWarning("Discord webhook rejected");
            _webhookUrl = null;
        }
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(!string.IsNullOrWhiteSpace(_webhookUrl), string.IsNullOrWhiteSpace(_webhookUrl) ? "Missing webhook" : "OK"));

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null || string.IsNullOrWhiteSpace(_webhookUrl))
            return new DeliveryResult { Success = false, ErrorCode = "CONFIG_MISSING", ErrorMessage = "Discord webhook not configured" };
        var content = string.IsNullOrWhiteSpace(notification.Subject)
            ? notification.Body
            : $"**{notification.Subject}**\n{notification.Body}";
        if (content.Length > 2000)
            content = content[..2000];
        try
        {
            using var resp = await _http.PostAsJsonAsync(_webhookUrl, new { content }, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = $"http_{(int)resp.StatusCode}", ErrorMessage = body };
            }
            return new DeliveryResult { Success = true, ProviderId = Id, ProviderMessageId = Guid.NewGuid().ToString("N") };
        }
        catch (Exception ex)
        {
            return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = "DISCORD_ERROR", ErrorMessage = ex.Message };
        }
    }

    public static bool IsSafeHttps(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u))
            return false;
        if (!string.Equals(u.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            return false;
        if (u.IsLoopback)
            return false;
        return u.Host.Contains("discord", StringComparison.OrdinalIgnoreCase) || u.Host.Contains("discordapp", StringComparison.OrdinalIgnoreCase);
    }
}

