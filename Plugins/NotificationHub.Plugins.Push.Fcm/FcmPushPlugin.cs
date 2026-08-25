using System.Net.Http.Json;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Push.Fcm;

public sealed class FcmPushPlugin : IChannelPlugin
{
    private string? _serverKey; private HttpClient? _http;
    public string Id => "push-fcm";
    public Version Version => new(1, 0, 0);
    public string Name => "Firebase Cloud Messaging";
    public string Channel => "push";
    public PluginCapability[] Capabilities => [new("channel", "push"), new("provider", "fcm")];
    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    { _serverKey = context.Configuration["Plugins:Fcm:ServerKey"]; _http = new HttpClient { BaseAddress = new Uri("https://fcm.googleapis.com/") }; return Task.CompletedTask; }
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(!string.IsNullOrWhiteSpace(_serverKey), string.IsNullOrWhiteSpace(_serverKey) ? "Missing ServerKey" : "OK"));
    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null || string.IsNullOrWhiteSpace(_serverKey))
            return new DeliveryResult { Success = false, ErrorCode = "CONFIG_MISSING", ErrorMessage = "FCM not configured" };
        using var req = new HttpRequestMessage(HttpMethod.Post, "fcm/send");
        req.Headers.TryAddWithoutValidation("Authorization", $"key={_serverKey}");
        req.Content = JsonContent.Create(new { to = notification.Recipient, notification = new { title = notification.Subject, body = notification.Body } });
        var resp = await _http.SendAsync(req, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        return new DeliveryResult { Success = resp.IsSuccessStatusCode, ProviderMessageId = resp.IsSuccessStatusCode ? Guid.NewGuid().ToString("N") : null, ErrorCode = resp.IsSuccessStatusCode ? null : resp.StatusCode.ToString(), ErrorMessage = resp.IsSuccessStatusCode ? null : body };
    }
}
