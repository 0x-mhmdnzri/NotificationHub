using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Chat.Slack;

public sealed class SlackPlugin : IChannelPlugin
{
    private string? _webhookUrl; private HttpClient? _http;
    public string Id => "chat-slack";
    public Version Version => new(1, 0, 0);
    public string Name => "Slack Incoming Webhook";
    public string Channel => "slack";
    public PluginCapability[] Capabilities => [new("channel", "slack")];
    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    { _webhookUrl = context.Configuration["Plugins:Slack:WebhookUrl"]; _http = new HttpClient(); return Task.CompletedTask; }
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(!string.IsNullOrWhiteSpace(_webhookUrl), string.IsNullOrWhiteSpace(_webhookUrl) ? "Missing webhook" : "OK"));
    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null || string.IsNullOrWhiteSpace(_webhookUrl))
            return new DeliveryResult { Success = false, ErrorCode = "CONFIG_MISSING", ErrorMessage = "Slack webhook not configured" };
        var resp = await _http.PostAsJsonAsync(_webhookUrl, new { text = $"*{notification.Subject}*\n{notification.Body}" }, cancellationToken);
        return new DeliveryResult { Success = resp.IsSuccessStatusCode, ProviderMessageId = Guid.NewGuid().ToString("N"), ErrorCode = resp.IsSuccessStatusCode ? null : resp.StatusCode.ToString() };
    }
}
