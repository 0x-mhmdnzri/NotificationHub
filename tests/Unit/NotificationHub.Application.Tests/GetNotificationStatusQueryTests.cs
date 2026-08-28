using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.DependencyInjection;
using NotificationHub.Application.Features.Notifications.Accept;
using NotificationHub.Application.Features.Notifications.GetStatus;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Application.Tests;

public class GetNotificationStatusQueryTests
{
    [Fact]
    public async Task Query_AfterAccept_ReturnsProjectedDto()
    {
        await using var db = TestFixtures.CreateDbContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<NotificationHub.Application.Abstractions.IRequestContext, TestRequestContext>();
        services.AddSingleton(TestFixtures.CreateOrchestrator(db));
        services.AddScoped(_ => db);
        var sp = services.BuildServiceProvider();
        var sender = sp.GetRequiredService<ISender>();

        var accepted = await sender.Send(new AcceptNotificationCommand(
            new NotificationRequest { Recipient = "q@b.com", Channel = "email", TemplateKey = "welcome" }, null));
        accepted.IsSuccess.Should().BeTrue();

        var query = await sender.Send(new GetNotificationStatusQuery(accepted.Value!.NotificationId, null, true));
        query.IsSuccess.Should().BeTrue();
        query.Value!.Recipient.Should().Be("q@b.com");
        query.Value.Status.Should().Be("Queued");
    }

    [Fact]
    public async Task Query_Missing_ReturnsNotFound()
    {
        await using var db = TestFixtures.CreateDbContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<NotificationHub.Application.Abstractions.IRequestContext, TestRequestContext>();
        services.AddSingleton(TestFixtures.CreateOrchestrator(db));
        services.AddScoped(_ => db);
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        var query = await sender.Send(new GetNotificationStatusQuery(Guid.NewGuid(), null, true));
        query.IsFailure.Should().BeTrue();
        query.Error!.Type.Should().Be(NotificationHub.Application.Abstractions.ErrorType.NotFound);
    }
}
