using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Email.Resend;

/// <summary>F17 — Resend email API.</summary>
public sealed class ResendEmailPlugin : IChannelPlugin
{
    private string? _apiKey, _from;
    private HttpClient? _http;

    public string Id => "email-resend";
    public Version Version => new(1, 0, 0);
    public string Name => "Resend Email";
    public string Channel => "email";
    public PluginCapability[] Capabilities => [new("channel", "email"), new("provider", "resend")];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _apiKey = context.Configuration["Plugins:Resend:ApiKey"];
        _from = context.Configuration["Plugins:Resend:From"] ?? "noreply@example.com";
        _http = new HttpClient { BaseAddress = new Uri("https://api.resend.com/"), Timeout = TimeSpan.FromSeconds(15) };
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(!string.IsNullOrWhiteSpace(_apiKey), string.IsNullOrWhiteSpace(_apiKey) ? "Missing ApiKey" : "OK"));

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null || string.IsNullOrWhiteSpace(_apiKey))
            return new DeliveryResult { Success = false, ErrorCode = "CONFIG_MISSING", ErrorMessage = "Resend not configured" };
        using var req = new HttpRequestMessage(HttpMethod.Post, "emails");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        req.Content = JsonContent.Create(new
        {
            from = _from,
            to = new[] { notification.Recipient },
            subject = notification.Subject ?? "(no subject)",
            text = notification.Body,
            html = notification.HtmlBody
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
            return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = "RESEND_ERROR", ErrorMessage = ex.Message };
        }
    }
}
