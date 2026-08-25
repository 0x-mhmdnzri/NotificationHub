using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Templates;

public sealed class TemplateSeeder
{
    private readonly ITemplateStore _store;
    private readonly ILogger<TemplateSeeder> _logger;

    public TemplateSeeder(ITemplateStore store, ILogger<TemplateSeeder> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task SeedDefaultsAsync(CancellationToken ct = default)
    {
        var defaults = new[]
        {
            new TemplateDefinition
            {
                Key = "welcome", Channel = "email", Locale = "en",
                Subject = "Welcome {{name}}!",
                Body = "Hello {{name}}, welcome to our service.",
                HtmlBody = "<h1>Hello {{name}}</h1><p>Welcome to our service.</p>"
            },
            new TemplateDefinition
            {
                Key = "welcome", Channel = "email", Locale = "fa",
                Subject = "خوش آمدید {{name}}!",
                Body = "سلام {{name}}، به سرویس ما خوش آمدید.",
                HtmlBody = "<h1>سلام {{name}}</h1><p>به سرویس ما خوش آمدید.</p>"
            },
            new TemplateDefinition
            {
                Key = "otp", Channel = "sms", Locale = "en",
                Subject = "OTP",
                Body = "Your verification code is {{code}}. Valid for {{minutes}} minutes."
            },
            new TemplateDefinition
            {
                Key = "otp", Channel = "sms", Locale = "fa",
                Subject = "کد تایید",
                Body = "کد تایید شما {{code}} است. اعتبار: {{minutes}} دقیقه."
            }
        };

        foreach (var template in defaults)
        {
            var existing = await _store.FindAsync(template.Key, template.Channel, template.Locale, null, ct);
            if (existing is null)
            {
                await _store.SaveAsync(template, ct);
                _logger.LogInformation("Seeded template {Key}/{Channel}/{Locale}", template.Key, template.Channel, template.Locale);
            }
        }
    }
}
