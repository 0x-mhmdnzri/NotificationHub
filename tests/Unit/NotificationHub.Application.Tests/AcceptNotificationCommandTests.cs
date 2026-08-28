using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.DependencyInjection;
using NotificationHub.Application.Features.Notifications.Accept;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Application.Tests;

public class AcceptNotificationCommandTests
{
    private static ServiceProvider BuildSp(NotificationHub.Core.Persistence.NotificationDbContext db)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<NotificationHub.Application.Abstractions.IRequestContext, TestRequestContext>();
        services.AddSingleton(TestFixtures.CreateOrchestrator(db));
        services.AddScoped(_ => db);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Command_ValidRequest_ReturnsQueued()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sender = BuildSp(db).GetRequiredService<ISender>();
        var result = await sender.Send(new AcceptNotificationCommand(
            new NotificationRequest { Recipient = "a@b.com", Channel = "email", TemplateKey = "welcome" }, null));
        result.IsSuccess.Should().BeTrue();
        result.Value!.Status.Should().Be("Queued");
    }

    [Fact]
    public async Task Command_MissingRecipient_FailsValidation()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sender = BuildSp(db).GetRequiredService<ISender>();
        // ValidationBehavior returns Result.Failure for Result handlers (not ValidationException)
        var result = await sender.Send(new AcceptNotificationCommand(
            new NotificationRequest { Recipient = "", Channel = "email", TemplateKey = "welcome" }, null));
        result.IsFailure.Should().BeTrue();
        result.Error!.Type.Should().Be(NotificationHub.Application.Abstractions.ErrorType.Validation);
    }
}
