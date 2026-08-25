using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Templates;

public sealed class InMemoryTemplateEngine : ITemplateEngine
{
    private readonly ConcurrentDictionary<string, TemplateDefinition> _templates = new();
    private readonly ILogger<InMemoryTemplateEngine> _logger;
    private static readonly Regex PlaceholderRegex = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public InMemoryTemplateEngine(ILogger<InMemoryTemplateEngine> logger)
    {
        _logger = logger;
        // Seed some default templates
        SeedDefaults();
    }

    private void SeedDefaults()
    {
        RegisterTemplateAsync(new TemplateDefinition
        {
            Key = "welcome",
            Channel = "email",
            Locale = "en",
            Subject = "Welcome {{name}}!",
            Body = "Hello {{name}}, welcome to our service.",
            HtmlBody = "<h1>Hello {{name}}</h1><p>Welcome to our service.</p>"
        }).GetAwaiter().GetResult();

        RegisterTemplateAsync(new TemplateDefinition
        {
            Key = "otp",
            Channel = "sms",
            Locale = "en",
            Subject = "OTP",
            Body = "Your verification code is {{code}}. Valid for {{minutes}} minutes."
        }).GetAwaiter().GetResult();

        RegisterTemplateAsync(new TemplateDefinition
        {
            Key = "welcome",
            Channel = "email",
            Locale = "fa",
            Subject = "خوش آمدید {{name}}!",
            Body = "سلام {{name}}، به سرویس ما خوش آمدید.",
            HtmlBody = "<h1>سلام {{name}}</h1><p>به سرویس ما خوش آمدید.</p>"
        }).GetAwaiter().GetResult();
    }

    public Task RegisterTemplateAsync(TemplateDefinition template, CancellationToken ct = default)
    {
        var key = BuildKey(template.Key, template.Channel, template.Locale, template.TenantId);
        _templates[key] = template;
        _logger.LogInformation("Registered template {Key} v{Version} for {Channel}/{Locale}", template.Key, template.Version, template.Channel, template.Locale);
        return Task.CompletedTask;
    }

    public Task<TemplateDefinition?> GetTemplateAsync(string key, string channel, string locale = "en", string? tenantId = null, CancellationToken ct = default)
    {
        // Try tenant-specific first, then global
        if (!string.IsNullOrEmpty(tenantId))
        {
            var tenantKey = BuildKey(key, channel, locale, tenantId);
            if (_templates.TryGetValue(tenantKey, out var tenantTemplate))
                return Task.FromResult<TemplateDefinition?>(tenantTemplate);
        }

        var globalKey = BuildKey(key, channel, locale, null);
        if (_templates.TryGetValue(globalKey, out var template))
            return Task.FromResult<TemplateDefinition?>(template);

        // Fallback to en
        if (locale != "en")
        {
            var enKey = BuildKey(key, channel, "en", null);
            if (_templates.TryGetValue(enKey, out var enTemplate))
                return Task.FromResult<TemplateDefinition?>(enTemplate);
        }

        return Task.FromResult<TemplateDefinition?>(null);
    }

    public async Task<RenderedNotification> RenderAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var channel = request.Channel ?? "email";
        var template = await GetTemplateAsync(request.TemplateKey, channel, request.Locale ?? "en", request.TenantId, ct);

        string subject;
        string body;
        string? htmlBody = null;

        if (template is null)
        {
            _logger.LogWarning("Template {Key} not found for {Channel}/{Locale}. Using raw key.", request.TemplateKey, request.Channel, request.Locale);
            subject = request.TemplateKey;
            body = $"Notification: {request.TemplateKey}";
        }
        else
        {
            subject = ReplacePlaceholders(template.Subject, request.Data);
            body = ReplacePlaceholders(template.Body, request.Data);
            htmlBody = template.HtmlBody is not null ? ReplacePlaceholders(template.HtmlBody, request.Data) : null;
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
            Locale = request.Locale
        };
    }

    private static string ReplacePlaceholders(string template, Dictionary<string, object?> data)
    {
        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return data.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : match.Value;
        });
    }

    private static string BuildKey(string key, string channel, string locale, string? tenantId)
        => $"{tenantId ?? "global"}:{channel}:{locale}:{key}".ToLowerInvariant();
}
