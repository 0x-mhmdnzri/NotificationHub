using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Email.SendGrid;

public sealed class SendGridEmailPlugin : IChannelPlugin
{
    private ILogger? _logger;
    private string? _apiKey;

    public string Id => "email-sendgrid";
    public Version Version => new(1, 0, 0);
    public string Name => "SendGrid Email Provider";
    public string Channel => "email";
    public PluginCapability[] Capabilities =>
    [
        new("channel", "email"),
        new("provider", "sendgrid"),
        new("supports-html", "true"),
        new("supports-attachments", "true")
    ];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.Logger;
        _apiKey = context.Configuration["Plugins:SendGrid:ApiKey"];
        _logger?.LogInformation("SendGrid plugin initialized. ApiKey present: {HasKey}", !string.IsNullOrEmpty(_apiKey));
        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var healthy = !string.IsNullOrEmpty(_apiKey);
        return Task.FromResult(new PluginHealth(healthy, healthy ? "OK" : "Missing API Key"));
    }

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        _logger?.LogInformation("[SendGrid STUB] Sending email to {Recipient}: {Subject}", notification.Recipient, notification.Subject);
        await Task.Delay(50, cancellationToken);

        if (string.IsNullOrEmpty(_apiKey))
        {
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "CONFIG_MISSING",
                ErrorMessage = "SendGrid API Key not configured"
            };
        }

        return new DeliveryResult
        {
            Success = true,
            ProviderMessageId = $"sg-stub-{Guid.NewGuid():N}"
        };
    }
}
