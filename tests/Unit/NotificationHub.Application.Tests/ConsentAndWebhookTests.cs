using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.DependencyInjection;
using NotificationHub.Application.Features.Consents.Record;
using NotificationHub.Application.Features.Webhooks.Create;
using NotificationHub.Core.Compliance;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Application.Tests;

public class ConsentAndWebhookTests
{
    [Fact]
    public async Task RecordConsent_Valid_Succeeds()
    {
        await using var db = TestFixtures.CreateDbContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<NotificationHub.Application.Abstractions.IRequestContext, TestRequestContext>();
        services.AddScoped<IConsentService, ConsentService>();
        services.AddScoped(_ => db);
        services.AddSingleton(TestFixtures.CreateOrchestrator(db));
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        var result = await sender.Send(new RecordConsentCommand(
            new ConsentRecord { SubjectId = "u1", Purpose = "marketing", Channel = "email", Granted = true },
            null));
        result.IsSuccess.Should().BeTrue();
        result.Value!.SubjectId.Should().Be("u1");
    }

    [Fact]
    public async Task CreateWebhook_UnsafeUrl_FailsValidation()
    {
        await using var db = TestFixtures.CreateDbContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<NotificationHub.Application.Abstractions.IRequestContext, TestRequestContext>();
        services.AddScoped(_ => db);
        services.AddSingleton(TestFixtures.CreateOrchestrator(db));
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        var result = await sender.Send(new CreateWebhookCommand(
            new WebhookSubscription { Url = "http://127.0.0.1/hook", Events = ["sent"] }, null));
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(NotificationHub.Application.Abstractions.ErrorType.Validation);
    }
}

