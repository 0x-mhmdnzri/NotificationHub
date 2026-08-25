using Kavenegar;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Sms.Kavenegar;

public sealed class KavenegarSmsPlugin : IChannelPlugin
{
    private ILogger? _logger;
    private string? _apiKey;
    private string? _sender;
    private KavenegarApi? _api;

    public string Id => "sms-kavenegar";
    public Version Version => new(1, 0, 0);
    public string Name => "Kavenegar SMS Provider";
    public string Channel => "sms";
    public PluginCapability[] Capabilities =>
    [
        new("channel", "sms"),
        new("provider", "kavenegar"),
        new("supports-delivery-report", "true"),
        new("region", "ir")
    ];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.Logger;
        _apiKey = context.Configuration["Plugins:Kavenegar:ApiKey"];
        _sender = context.Configuration["Plugins:Kavenegar:Sender"];

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _api = new KavenegarApi(_apiKey);
            _logger?.LogInformation("Kavenegar plugin initialized. Sender={Sender}", _sender);
        }
        else
        {
            _logger?.LogWarning("Kavenegar ApiKey is missing. Plugin will fail on send.");
        }

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var healthy = _api is not null;
        return Task.FromResult(new PluginHealth(healthy, healthy ? "OK" : "Missing Kavenegar API Key"));
    }

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_api is null)
        {
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "CONFIG_MISSING",
                ErrorMessage = "Kavenegar API Key not configured"
            };
        }

        try
        {
            // SDK is synchronous
            var result = await Task.Run(() => _api.Send(_sender, notification.Recipient, notification.Body), cancellationToken);

            if (result is null)
            {
                return new DeliveryResult
                {
                    Success = false,
                    ErrorCode = "NULL_RESPONSE",
                    ErrorMessage = "Kavenegar returned null"
                };
            }

            // Treat any non-null response with Messageid as accepted by provider
            var messageId = result.Messageid.ToString();
            var statusText = result.Status.ToString();

            _logger?.LogInformation("Kavenegar SMS to {Recipient}, MessageId={MessageId}, Status={Status}",
                notification.Recipient, messageId, statusText);

            // Kavenegar returns status codes; 1=queued/sent-ish, 5=delivered in many versions.
            // We consider it successful if we got a message id back.
            return new DeliveryResult
            {
                Success = !string.IsNullOrEmpty(messageId) && messageId != "0",
                ProviderMessageId = messageId,
                ErrorCode = null,
                ErrorMessage = statusText
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Kavenegar send failed for {Recipient}", notification.Recipient);
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "EXCEPTION",
                ErrorMessage = ex.Message
            };
        }
    }
}
