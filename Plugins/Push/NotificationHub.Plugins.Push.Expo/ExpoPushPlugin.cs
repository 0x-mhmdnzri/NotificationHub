using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Push.Expo;

/// <summary>F18 — Expo Push API.</summary>
public sealed class ExpoPushPlugin : IChannelPlugin
{
    private string? _accessToken;
    private HttpClient? _http;

    public string Id => "push-expo";
    public Version Version => new(1, 0, 0);
    public string Name => "Expo Push";
    public string Channel => "push";
    public PluginCapability[] Capabilities => [new("channel", "push"), new("provider", "expo")];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _accessToken = context.Configuration["Plugins:Expo:AccessToken"]; // optional
        _http = new HttpClient { BaseAddress = new Uri("https://exp.host/--/api/v2/"), Timeout = TimeSpan.FromSeconds(15) };
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(true, "Expo endpoint ready"));

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null)
            return new DeliveryResult { Success = false, ErrorCode = "CONFIG_MISSING" };

        var token = notification.Recipient;
        if (string.IsNullOrWhiteSpace(token) || (!token.StartsWith("ExponentPushToken") && !token.StartsWith("ExpoPushToken")))
            return new DeliveryResult { Success = false, ErrorCode = "INVALID_TOKEN", ErrorMessage = "Recipient must be Expo push token" };

        using var req = new HttpRequestMessage(HttpMethod.Post, "push/send");
        if (!string.IsNullOrWhiteSpace(_accessToken))
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_accessToken}");
        req.Content = JsonContent.Create(new[]
        {
            new
            {
                to = token,
                title = notification.Subject,
                body = notification.Body,
                sound = "default"
            }
        });
        try
        {
            using var resp = await _http.SendAsync(req, cancellationToken);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken);
            return new DeliveryResult
            {
                Success = resp.IsSuccessStatusCode,
                ProviderId = Id,
                ProviderMessageId = resp.IsSuccessStatusCode ? Guid.NewGuid().ToString("N") : null,
                ErrorCode = resp.IsSuccessStatusCode ? null : $"http_{(int)resp.StatusCode}",
                ErrorMessage = resp.IsSuccessStatusCode ? null : body
            };
        }
        catch (Exception ex)
        {
            return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = "EXPO_ERROR", ErrorMessage = ex.Message };
        }
    }
}
