using System.Net.Http.Json;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Chat.Teams;

/// <summary>F16 — Microsoft Teams incoming webhook.</summary>
public sealed class TeamsPlugin : IChannelPlugin
{
    private string? _webhookUrl;
    private HttpClient? _http;

    public string Id => "chat-teams";
    public Version Version => new(1, 0, 0);
    public string Name => "Microsoft Teams Webhook";
    public string Channel => "teams";
    public PluginCapability[] Capabilities => [new("channel", "teams")];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        var url = context.Configuration["Plugins:Teams:WebhookUrl"];
        if (!string.IsNullOrWhiteSpace(url)
            && Uri.TryCreate(url, UriKind.Absolute, out var u)
            && u.Scheme == Uri.UriSchemeHttps
            && !u.IsLoopback)
        {
            _webhookUrl = url;
        }
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(!string.IsNullOrWhiteSpace(_webhookUrl),
            string.IsNullOrWhiteSpace(_webhookUrl) ? "Missing webhook" : "OK"));

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null || string.IsNullOrWhiteSpace(_webhookUrl))
            return new DeliveryResult { Success = false, ErrorCode = "CONFIG_MISSING", ErrorMessage = "Teams webhook not configured" };
        var payload = new
        {
            text = string.IsNullOrWhiteSpace(notification.Subject)
                ? notification.Body
                : $"**{notification.Subject}**\n\n{notification.Body}"
        };
        try
        {
            using var resp = await _http.PostAsJsonAsync(_webhookUrl, payload, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(cancellationToken);
                return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = $"http_{(int)resp.StatusCode}", ErrorMessage = body };
            }
            return new DeliveryResult { Success = true, ProviderId = Id, ProviderMessageId = Guid.NewGuid().ToString("N") };
        }
        catch (Exception ex)
        {
            return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = "TEAMS_ERROR", ErrorMessage = ex.Message };
        }
    }
}
