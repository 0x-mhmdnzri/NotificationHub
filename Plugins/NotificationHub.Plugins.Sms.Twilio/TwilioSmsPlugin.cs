using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace NotificationHub.Plugins.Sms.Twilio;

public sealed class TwilioSmsPlugin : IChannelPlugin
{
    private ILogger? _logger;
    private string? _accountSid;
    private string? _authToken;
    private string? _fromNumber;
    private bool _initialized;

    public string Id => "sms-twilio";
    public Version Version => new(1, 0, 0);
    public string Name => "Twilio SMS Provider";
    public string Channel => "sms";
    public PluginCapability[] Capabilities =>
    [
        new("channel", "sms"),
        new("provider", "twilio"),
        new("supports-delivery-report", "true")
    ];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.Logger;
        _accountSid = context.Configuration["Plugins:Twilio:AccountSid"];
        _authToken = context.Configuration["Plugins:Twilio:AuthToken"];
        _fromNumber = context.Configuration["Plugins:Twilio:FromNumber"];

        if (!string.IsNullOrWhiteSpace(_accountSid) && !string.IsNullOrWhiteSpace(_authToken))
        {
            TwilioClient.Init(_accountSid, _authToken);
            _initialized = true;
            _logger?.LogInformation("Twilio plugin initialized. From={From}", _fromNumber);
        }
        else
        {
            _logger?.LogWarning("Twilio credentials missing. Plugin will fail on send.");
        }

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var healthy = _initialized && !string.IsNullOrWhiteSpace(_fromNumber);
        return Task.FromResult(new PluginHealth(healthy, healthy ? "OK" : "Missing Twilio credentials or FromNumber"));
    }

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (!_initialized || string.IsNullOrWhiteSpace(_fromNumber))
        {
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "CONFIG_MISSING",
                ErrorMessage = "Twilio credentials or FromNumber not configured"
            };
        }

        try
        {
            var message = await MessageResource.CreateAsync(
                to: new PhoneNumber(notification.Recipient),
                from: new PhoneNumber(_fromNumber),
                body: notification.Body
            );

            _logger?.LogInformation("Twilio SMS sent to {Recipient}, Sid={Sid}, Status={Status}",
                notification.Recipient, message.Sid, message.Status);

            return new DeliveryResult
            {
                Success = message.Status != MessageResource.StatusEnum.Failed &&
                          message.Status != MessageResource.StatusEnum.Undelivered,
                ProviderMessageId = message.Sid,
                ErrorCode = message.ErrorCode?.ToString(),
                ErrorMessage = message.ErrorMessage
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Twilio send failed for {Recipient}", notification.Recipient);
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "EXCEPTION",
                ErrorMessage = ex.Message
            };
        }
    }
}
