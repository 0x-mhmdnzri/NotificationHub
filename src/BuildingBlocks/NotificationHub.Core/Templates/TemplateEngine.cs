using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Templates;

/// <summary>
/// Orchestrates template lookup + rendering. Depends on abstractions (DIP).
/// Storage and rendering strategies are swappable (OCP).
/// </summary>
public sealed class TemplateEngine : ITemplateEngine
{
    private readonly ITemplateStore _store;
    private readonly ITemplateRenderer _renderer;
    private readonly ILogger<TemplateEngine> _logger;

    public TemplateEngine(ITemplateStore store, ITemplateRenderer renderer, ILogger<TemplateEngine> logger)
    {
        _store = store;
        _renderer = renderer;
        _logger = logger;
    }

    public Task RegisterTemplateAsync(TemplateDefinition template, CancellationToken ct = default)
        => _store.SaveAsync(template, ct);

    public Task<TemplateDefinition?> GetTemplateAsync(string key, string channel, string locale = "en", string? tenantId = null, CancellationToken ct = default)
        => _store.FindAsync(key, channel, locale, tenantId, ct);

    public async Task<RenderedNotification> RenderAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var channel = request.Channel ?? "email";
        var template = await _store.FindAsync(request.TemplateKey, channel, request.Locale ?? "en", request.TenantId, ct);

        string subject;
        string body;
        string? htmlBody = null;

        if (template is null)
        {
            _logger.LogWarning("Template {Key} not found for {Channel}/{Locale}. Using raw key.",
                request.TemplateKey, channel, request.Locale);
            subject = request.TemplateKey;
            body = $"Notification: {request.TemplateKey}";
        }
        else
        {
            subject = _renderer.Render(template.Subject, request.Data);
            body = _renderer.Render(template.Body, request.Data);
            htmlBody = template.HtmlBody is not null ? _renderer.Render(template.HtmlBody, request.Data) : null;
        }

        return new RenderedNotification
        {
            NotificationId = request.Id,
            Recipient = request.Recipient,
            Channel = channel,
            Subject = subject,
            Body = body,
            HtmlBody = htmlBody,
            TenantId = request.TenantId,
            Locale = request.Locale,
            PreferredProvider = request.PreferredProvider,
            Attachments = request.Attachments
        };
    }
}
