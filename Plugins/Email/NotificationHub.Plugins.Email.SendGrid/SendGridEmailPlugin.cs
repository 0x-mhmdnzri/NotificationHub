using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using SendGrid;
using SendGrid.Helpers.Mail;

namespace NotificationHub.Plugins.Email.SendGrid;

public sealed class SendGridEmailPlugin : IChannelPlugin
{
    private ILogger? _logger;
    private string? _apiKey;
    private string? _fromEmail;
    private string? _fromName;
    private SendGridClient? _client;

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
        _fromEmail = context.Configuration["Plugins:SendGrid:FromEmail"] ?? "noreply@example.com";
        _fromName = context.Configuration["Plugins:SendGrid:FromName"] ?? "NotificationHub";

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            _client = new SendGridClient(_apiKey);
            _logger?.LogInformation("SendGrid plugin initialized (API key configured, from-address present={HasFrom}).", !string.IsNullOrWhiteSpace(_fromEmail));
        }
        else
        {
            _logger?.LogWarning("SendGrid ApiKey is missing. Plugin will fail on send.");
        }

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var healthy = _client is not null;
        return Task.FromResult(new PluginHealth(healthy, healthy ? "OK" : "Missing API Key"));
    }

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "CONFIG_MISSING",
                ErrorMessage = "SendGrid API Key not configured"
            };
        }

        try
        {
            var msg = new SendGridMessage
            {
                From = new EmailAddress(_fromEmail, _fromName),
                Subject = notification.Subject,
                PlainTextContent = notification.Body,
                HtmlContent = notification.HtmlBody ?? notification.Body
            };

            msg.AddTo(new EmailAddress(notification.Recipient));

            if (notification.Attachments is { Count: > 0 })
            {
                foreach (var att in notification.Attachments)
                {
                    msg.AddAttachment(att.FileName, Convert.ToBase64String(att.Content), att.ContentType);
                }
            }

            var response = await _client.SendEmailAsync(msg, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var messageId = response.Headers.TryGetValues("X-Message-Id", out var values)
                    ? values.FirstOrDefault()
                    : null;

                _logger?.LogInformation("SendGrid email sent to {Recipient}, MessageId={MessageId}",
                    notification.Recipient, messageId);

                return new DeliveryResult
                {
                    Success = true,
                    ProviderMessageId = messageId
                };
            }

            var body = await response.Body.ReadAsStringAsync(cancellationToken);
            _logger?.LogWarning("SendGrid failed: {Status} {Body}", response.StatusCode, body);

            return new DeliveryResult
            {
                Success = false,
                ErrorCode = response.StatusCode.ToString(),
                ErrorMessage = body
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SendGrid send failed for {Recipient}", notification.Recipient);
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "EXCEPTION",
                ErrorMessage = ex.Message
            };
        }
    }
}
