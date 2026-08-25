using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Templates;

namespace NotificationHub.Core.Tests.Templates;

public class TemplateEngineTests
{
    private readonly InMemoryTemplateEngine _sut = new(NullLogger<InMemoryTemplateEngine>.Instance);

    [Fact]
    public async Task TC_F_001_Render_WelcomeEmail_ReplacesVariables()
    {
        var request = new NotificationRequest
        {
            Recipient = "a@b.com",
            Channel = "email",
            TemplateKey = "welcome",
            Locale = "en",
            Data = new Dictionary<string, object?> { ["name"] = "Ali" }
        };

        var rendered = await _sut.RenderAsync(request);

        rendered.Subject.Should().Be("Welcome Ali!");
        rendered.Body.Should().Contain("Ali");
        rendered.Channel.Should().Be("email");
    }

    [Fact]
    public async Task TC_F_002_Render_PersianLocale_UsesFaTemplate()
    {
        var request = new NotificationRequest
        {
            Recipient = "a@b.com",
            Channel = "email",
            TemplateKey = "welcome",
            Locale = "fa",
            Data = new Dictionary<string, object?> { ["name"] = "علی" }
        };

        var rendered = await _sut.RenderAsync(request);

        rendered.Subject.Should().Contain("علی");
        rendered.Body.Should().Contain("علی");
    }

    [Fact]
    public async Task TC_E_001_Render_UnknownTemplate_FallsBackToRawKey()
    {
        var request = new NotificationRequest
        {
            Recipient = "a@b.com",
            Channel = "email",
            TemplateKey = "missing-template",
            Data = new()
        };

        var rendered = await _sut.RenderAsync(request);

        rendered.Subject.Should().Be("missing-template");
        rendered.Body.Should().Contain("missing-template");
    }

    [Fact]
    public async Task TC_E_002_Render_MissingPlaceholderData_KeepsPlaceholder()
    {
        await _sut.RegisterTemplateAsync(new TemplateDefinition
        {
            Key = "custom",
            Channel = "sms",
            Locale = "en",
            Subject = "OTP",
            Body = "Code {{code}} for {{user}}"
        });

        var request = new NotificationRequest
        {
            Recipient = "+98912",
            Channel = "sms",
            TemplateKey = "custom",
            Data = new Dictionary<string, object?> { ["code"] = "1234" }
        };

        var rendered = await _sut.RenderAsync(request);

        rendered.Body.Should().Contain("1234");
        rendered.Body.Should().Contain("{{user}}");
    }

    [Fact]
    public async Task TC_F_003_GetTemplate_TenantSpecific_TakesPrecedence()
    {
        await _sut.RegisterTemplateAsync(new TemplateDefinition
        {
            Key = "invoice", Channel = "email", Locale = "en", TenantId = "t1",
            Subject = "Tenant Invoice", Body = "Tenant body"
        });
        await _sut.RegisterTemplateAsync(new TemplateDefinition
        {
            Key = "invoice", Channel = "email", Locale = "en",
            Subject = "Global Invoice", Body = "Global body"
        });

        var tenant = await _sut.GetTemplateAsync("invoice", "email", "en", "t1");
        var global = await _sut.GetTemplateAsync("invoice", "email", "en", null);

        tenant!.Subject.Should().Be("Tenant Invoice");
        global!.Subject.Should().Be("Global Invoice");
    }
}
