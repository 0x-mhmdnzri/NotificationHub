using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Templates;

namespace NotificationHub.Core.Tests.Templates;

public class TemplateEngineTests
{
    private static TemplateEngine CreateSut(out InMemoryTemplateStore store)
    {
        store = new InMemoryTemplateStore();
        // seed like production seeder
        store.SaveAsync(new TemplateDefinition
        {
            Key = "welcome", Channel = "email", Locale = "en",
            Subject = "Welcome {{name}}!", Body = "Hello {{name}}, welcome to our service.",
            HtmlBody = "<h1>Hello {{name}}</h1>"
        }).GetAwaiter().GetResult();
        store.SaveAsync(new TemplateDefinition
        {
            Key = "welcome", Channel = "email", Locale = "fa",
            Subject = "خوش آمدید {{name}}!", Body = "سلام {{name}}، به سرویس ما خوش آمدید."
        }).GetAwaiter().GetResult();
        return new TemplateEngine(store, new PlaceholderTemplateRenderer(), NullLogger<TemplateEngine>.Instance);
    }

    [Fact]
    public async Task TC_F_001_Render_WelcomeEmail_ReplacesVariables()
    {
        var sut = CreateSut(out _);
        var rendered = await sut.RenderAsync(new NotificationRequest
        {
            Recipient = "a@b.com", Channel = "email", TemplateKey = "welcome", Locale = "en",
            Data = new Dictionary<string, object?> { ["name"] = "Ali" }
        });
        rendered.Subject.Should().Be("Welcome Ali!");
        rendered.Body.Should().Contain("Ali");
    }

    [Fact]
    public async Task TC_F_002_Render_PersianLocale_UsesFaTemplate()
    {
        var sut = CreateSut(out _);
        var rendered = await sut.RenderAsync(new NotificationRequest
        {
            Recipient = "a@b.com", Channel = "email", TemplateKey = "welcome", Locale = "fa",
            Data = new Dictionary<string, object?> { ["name"] = "علی" }
        });
        rendered.Subject.Should().Contain("علی");
    }

    [Fact]
    public async Task TC_E_001_Render_UnknownTemplate_FallsBackToRawKey()
    {
        var sut = CreateSut(out _);
        var rendered = await sut.RenderAsync(new NotificationRequest
        {
            Recipient = "a@b.com", Channel = "email", TemplateKey = "missing-template", Data = new()
        });
        rendered.Subject.Should().Be("missing-template");
    }

    [Fact]
    public async Task TC_E_002_Render_MissingPlaceholderData_KeepsPlaceholder()
    {
        var sut = CreateSut(out var store);
        await store.SaveAsync(new TemplateDefinition
        {
            Key = "custom", Channel = "sms", Locale = "en", Subject = "OTP", Body = "Code {{code}} for {{user}}"
        });
        var rendered = await sut.RenderAsync(new NotificationRequest
        {
            Recipient = "+98912", Channel = "sms", TemplateKey = "custom",
            Data = new Dictionary<string, object?> { ["code"] = "1234" }
        });
        rendered.Body.Should().Contain("1234");
        rendered.Body.Should().Contain("{{user}}");
    }

    [Fact]
    public async Task TC_F_003_GetTemplate_TenantSpecific_TakesPrecedence()
    {
        var sut = CreateSut(out var store);
        await store.SaveAsync(new TemplateDefinition
        {
            Key = "invoice", Channel = "email", Locale = "en", TenantId = "t1",
            Subject = "Tenant Invoice", Body = "Tenant body"
        });
        await store.SaveAsync(new TemplateDefinition
        {
            Key = "invoice", Channel = "email", Locale = "en",
            Subject = "Global Invoice", Body = "Global body"
        });
        var tenant = await sut.GetTemplateAsync("invoice", "email", "en", "t1");
        var global = await sut.GetTemplateAsync("invoice", "email", "en", null);
        tenant!.Subject.Should().Be("Tenant Invoice");
        global!.Subject.Should().Be("Global Invoice");
    }

    [Fact]
    public async Task TC_F_004_PostgresStore_RoundTrip_ViaInMemoryContract()
    {
        // Contract test on ITemplateStore (DIP): any store implementation must honor save/find/delete
        ITemplateStore store = new InMemoryTemplateStore();
        await store.SaveAsync(new TemplateDefinition
        {
            Key = "x", Channel = "email", Locale = "en", Subject = "S", Body = "B"
        });
        var found = await store.FindAsync("x", "email", "en", null);
        found.Should().NotBeNull();
        var deleted = await store.DeleteAsync("x", "email", "en", null);
        deleted.Should().BeTrue();
    }
}
