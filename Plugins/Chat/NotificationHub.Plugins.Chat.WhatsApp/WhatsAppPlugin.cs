using System.Net.Http.Headers;
using System.Net.Http.Json;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Chat.WhatsApp;

public sealed class WhatsAppPlugin : IChannelPlugin
{
    private string? _token; private string? _phoneNumberId; private HttpClient? _http;
    public string Id => "chat-whatsapp";
    public Version Version => new(1, 0, 0);
    public string Name => "WhatsApp Cloud API";
    public string Channel => "whatsapp";
    public PluginCapability[] Capabilities => [new("channel", "whatsapp"), new("provider", "meta")];
    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _token = context.Configuration["Plugins:WhatsApp:AccessToken"];
        _phoneNumberId = context.Configuration["Plugins:WhatsApp:PhoneNumberId"];
        _http = new HttpClient { BaseAddress = new Uri("https://graph.facebook.com/v19.0/") };
        if (!string.IsNullOrWhiteSpace(_token))
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return Task.CompletedTask;
    }
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(!string.IsNullOrWhiteSpace(_token) && !string.IsNullOrWhiteSpace(_phoneNumberId), "check config"));
    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null || string.IsNullOrWhiteSpace(_token) || string.IsNullOrWhiteSpace(_phoneNumberId))
            return new DeliveryResult { Success = false, ErrorCode = "CONFIG_MISSING", ErrorMessage = "WhatsApp not configured" };
        var payload = new { messaging_product = "whatsapp", to = notification.Recipient, type = "text", text = new { body = notification.Body } };
        var resp = await _http.PostAsJsonAsync($"{_phoneNumberId}/messages", payload, cancellationToken);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken);
        return new DeliveryResult { Success = resp.IsSuccessStatusCode, ProviderMessageId = resp.IsSuccessStatusCode ? Guid.NewGuid().ToString("N") : null, ErrorCode = resp.IsSuccessStatusCode ? null : resp.StatusCode.ToString(), ErrorMessage = resp.IsSuccessStatusCode ? null : body };
    }
}
