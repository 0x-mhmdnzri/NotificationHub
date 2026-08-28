using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Sms.Twilio;

/// <summary>F17 — Twilio SMS via REST API.</summary>
public sealed class TwilioSmsPlugin : IChannelPlugin
{
    private string? _accountSid, _authToken, _from;
    private HttpClient? _http;
    private ILogger? _logger;

    public string Id => "sms-twilio";
    public Version Version => new(1, 0, 0);
    public string Name => "Twilio SMS";
    public string Channel => "sms";
    public PluginCapability[] Capabilities => [new("channel", "sms"), new("provider", "twilio")];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.Logger;
        _accountSid = context.Configuration["Plugins:Twilio:AccountSid"];
        _authToken = context.Configuration["Plugins:Twilio:AuthToken"];
        _from = context.Configuration["Plugins:Twilio:From"];
        _http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(
            !string.IsNullOrWhiteSpace(_accountSid) && !string.IsNullOrWhiteSpace(_authToken) && !string.IsNullOrWhiteSpace(_from),
            "OK"));

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null || string.IsNullOrWhiteSpace(_accountSid) || string.IsNullOrWhiteSpace(_authToken) || string.IsNullOrWhiteSpace(_from))
            return new DeliveryResult { Success = false, ErrorCode = "CONFIG_MISSING", ErrorMessage = "Twilio not configured" };

        var url = $"https://api.twilio.com/2010-04-01/Accounts/{_accountSid}/Messages.json";
        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        var auth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_accountSid}:{_authToken}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["To"] = notification.Recipient,
            ["From"] = _from!,
            ["Body"] = notification.Body.Length > 1600 ? notification.Body[..1600] : notification.Body
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
            _logger?.LogError(ex, "Twilio send failed");
            return new DeliveryResult { Success = false, ProviderId = Id, ErrorCode = "TWILIO_ERROR", ErrorMessage = ex.Message };
        }
    }
}
