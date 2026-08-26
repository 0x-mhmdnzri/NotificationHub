using FluentAssertions;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.DependencyInjection;
using NotificationHub.Application.Notifications.Commands.AcceptNotification;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Application.Tests;

public class AcceptNotificationCommandTests
{
    private static ServiceProvider BuildSp(NotificationHub.Core.Persistence.NotificationDbContext db)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddSingleton(TestFixtures.CreateOrchestrator(db));
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Command_ValidRequest_ReturnsQueued()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sender = BuildSp(db).GetRequiredService<ISender>();
        var result = await sender.Send(new AcceptNotificationCommand(
            new NotificationRequest { Recipient = "a@b.com", Channel = "email", TemplateKey = "welcome" }, null));
        result.Accepted.Should().BeTrue();
        result.Status.Status.Should().Be(DeliveryStatus.Queued);
    }

    [Fact]
    public async Task Command_MissingRecipient_FailsValidation()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sender = BuildSp(db).GetRequiredService<ISender>();
        var act = async () => await sender.Send(new AcceptNotificationCommand(
            new NotificationRequest { Recipient = "", Channel = "email", TemplateKey = "welcome" }, null));
        await act.Should().ThrowAsync<ValidationException>();
    }
}
