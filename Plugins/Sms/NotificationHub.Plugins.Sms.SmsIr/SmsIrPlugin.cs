using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Sms.SmsIr;

public sealed class SmsIrPlugin : IChannelPlugin
{
    private ILogger? _logger;
    private string? _apiKey;
    private string? _lineNumber;
    private HttpClient? _http;

    public string Id => "sms-smsir";
    public Version Version => new(1, 0, 0);
    public string Name => "Sms.ir SMS Provider";
    public string Channel => "sms";
    public PluginCapability[] Capabilities =>
    [
        new("channel", "sms"),
        new("provider", "smsir"),
        new("region", "ir")
    ];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.Logger;
        _apiKey = context.Configuration["Plugins:SmsIr:ApiKey"];
        _lineNumber = context.Configuration["Plugins:SmsIr:LineNumber"];
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _http = new HttpClient { BaseAddress = new Uri("https://api.sms.ir/v1/") };
            _http.DefaultRequestHeaders.Add("x-api-key", _apiKey);
            _logger?.LogInformation("Sms.ir plugin initialized");
        }
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new PluginHealth(_http is not null, _http is not null ? "OK" : "Missing API Key"));

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_http is null)
            return new DeliveryResult { Success = false, ErrorCode = "CONFIG_MISSING", ErrorMessage = "Sms.ir API Key not configured" };
        try
        {
            var payload = new { lineNumber = long.TryParse(_lineNumber, out var ln) ? ln : 0L, messageText = notification.Body, mobiles = new[] { notification.Recipient }, sendDateTime = (string?)null };
            var response = await _http.PostAsJsonAsync("send/bulk", payload, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new DeliveryResult { Success = false, ErrorCode = response.StatusCode.ToString(), ErrorMessage = body };
            var parsed = System.Text.Json.JsonSerializer.Deserialize<SmsIrResponse>(body);
            return new DeliveryResult { Success = parsed?.Status == 1, ProviderMessageId = parsed?.Data?.PackId, ErrorMessage = parsed?.Message };
        }
        catch (Exception ex)
        {
            return new DeliveryResult { Success = false, ErrorCode = "EXCEPTION", ErrorMessage = ex.Message };
        }
    }

    private sealed class SmsIrResponse
    {
        [JsonPropertyName("status")] public int Status { get; set; }
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("data")] public SmsIrData? Data { get; set; }
    }
    private sealed class SmsIrData { [JsonPropertyName("packId")] public string? PackId { get; set; } }
}
