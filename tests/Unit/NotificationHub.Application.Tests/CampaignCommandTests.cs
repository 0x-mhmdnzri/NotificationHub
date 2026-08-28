using FluentAssertions;
using NotificationHub.Application.Abstractions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.DependencyInjection;
using NotificationHub.Application.Features.Campaigns.Create;
using NotificationHub.Application.Features.Campaigns.AddRecipients;
using NotificationHub.Application.Features.Campaigns.Start;
using NotificationHub.Application.Features.Campaigns.GetProgress;
using NotificationHub.Core.Campaigns;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Application.Tests;

public class CampaignCommandTests
{
    private static ServiceProvider Build(NotificationHub.Core.Persistence.NotificationDbContext db)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<IRequestContext, TestRequestContext>();
        services.AddSingleton(TestFixtures.CreateOrchestrator(db));
        services.AddScoped(_ => db);
        services.AddScoped<ICampaignService, CampaignService>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task Create_Add_Start_Progress()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sender = Build(db).GetRequiredService<ISender>();

        var created = await sender.Send(new CreateCampaignCommand(
            new CreateCampaignRequest
            {
                Name = "Ads Q1",
                TemplateKey = "welcome",
                Channels = ["sms", "email"]
            }, null, "test"));
        created.IsSuccess.Should().BeTrue();

        var added = await sender.Send(new AddRecipientsCommand(
            created.Value!.Id,
            new AddRecipientsRequest { Addresses = ["+989121111111", "a@b.com"] },
            null));
        added.IsSuccess.Should().BeTrue();
        // 2 addresses × 2 channels = 4
        added.Value.Should().Be(4);

        var started = await sender.Send(new StartCampaignCommand(created.Value.Id, null));
        started.IsSuccess.Should().BeTrue();

        var progress = await sender.Send(new GetCampaignProgressQuery(created.Value.Id, null));
        progress.IsSuccess.Should().BeTrue();
        progress.Value!.Total.Should().Be(4);
        progress.Value.Status.Should().Be(CampaignStatus.Processing);
    }
}
