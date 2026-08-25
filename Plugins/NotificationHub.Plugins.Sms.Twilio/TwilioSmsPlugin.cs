using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Sms.Twilio;

public sealed class TwilioSmsPlugin : IChannelPlugin
{
    private ILogger? _logger;
    private string? _accountSid;
    private string? _authToken;

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
        _logger?.LogInformation("Twilio plugin initialized. AccountSid present: {HasSid}", !string.IsNullOrEmpty(_accountSid));
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var healthy = !string.IsNullOrEmpty(_accountSid) && !string.IsNullOrEmpty(_authToken);
        return Task.FromResult(new PluginHealth(healthy, healthy ? "OK" : "Missing Twilio credentials"));
    }

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[Twilio STUB] Sending SMS to {Recipient}: {Body}", notification.Recipient, notification.Body);
        await Task.Delay(50, cancellationToken);

        if (string.IsNullOrEmpty(_accountSid) || string.IsNullOrEmpty(_authToken))
        {
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "CONFIG_MISSING",
                ErrorMessage = "Twilio credentials not configured"
            };
        }

        return new DeliveryResult
        {
            Success = true,
            ProviderMessageId = $"twilio-stub-{Guid.NewGuid():N}"
        };
    }
}
