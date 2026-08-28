using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;

namespace NotificationHub.Plugins.Email.Smtp;

public sealed class SmtpEmailPlugin : IChannelPlugin
{
    private ILogger? _logger;
    private string? _host;
    private int _port;
    private string? _username;
    private string? _password;
    private bool _enableSsl;
    private string? _fromEmail;
    private string? _fromName;

    public string Id => "email-smtp";
    public Version Version => new(1, 0, 0);
    public string Name => "SMTP Email Provider";
    public string Channel => "email";
    public PluginCapability[] Capabilities =>
    [
        new("channel", "email"),
        new("provider", "smtp"),
        new("supports-html", "true"),
        new("supports-attachments", "true")
    ];

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    {
        _logger = context.Logger;
        _host = context.Configuration["Plugins:Smtp:Host"];
        _port = int.TryParse(context.Configuration["Plugins:Smtp:Port"], out var p) ? p : 587;
        _username = context.Configuration["Plugins:Smtp:Username"];
        _password = context.Configuration["Plugins:Smtp:Password"];
        _enableSsl = !bool.TryParse(context.Configuration["Plugins:Smtp:EnableSsl"], out var ssl) || ssl;
        _fromEmail = context.Configuration["Plugins:Smtp:FromEmail"] ?? _username;
        _fromName = context.Configuration["Plugins:Smtp:FromName"] ?? "NotificationHub";

        if (string.IsNullOrWhiteSpace(_host))
            _logger?.LogWarning("SMTP Host is missing. Plugin will fail on send.");
        else
            _logger?.LogInformation("SMTP plugin initialized. Host={Host}:{Port}", _host, _port);

        return Task.CompletedTask;
    }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        var healthy = !string.IsNullOrWhiteSpace(_host) && !string.IsNullOrWhiteSpace(_fromEmail);
        return Task.FromResult(new PluginHealth(healthy, healthy ? "OK" : "Missing SMTP Host or FromEmail"));
    }

    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_host) || string.IsNullOrWhiteSpace(_fromEmail))
        {
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "CONFIG_MISSING",
                ErrorMessage = "SMTP Host or FromEmail not configured"
            };
        }

        try
        {
            using var client = new SmtpClient(_host, _port)
            {
                EnableSsl = _enableSsl,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(_username))
            {
                client.Credentials = new NetworkCredential(_username, _password);
            }

            using var message = new MailMessage
            {
                From = new MailAddress(_fromEmail!, _fromName),
                Subject = notification.Subject,
                Body = notification.HtmlBody ?? notification.Body,
                IsBodyHtml = !string.IsNullOrWhiteSpace(notification.HtmlBody)
            };

            message.To.Add(notification.Recipient);

            if (notification.Attachments is { Count: > 0 })
            {
                foreach (var att in notification.Attachments)
                {
                    var stream = new MemoryStream(att.Content);
                    message.Attachments.Add(new Attachment(stream, att.FileName, att.ContentType));
                }
            }

            await client.SendMailAsync(message, cancellationToken);

            _logger?.LogInformation("SMTP email sent to {Recipient}", notification.Recipient);

            return new DeliveryResult
            {
                Success = true,
                ProviderMessageId = Guid.NewGuid().ToString("N")
            };
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "SMTP send failed for {Recipient}", notification.Recipient);
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "EXCEPTION",
                ErrorMessage = ex.Message
            };
        }
    }
}
