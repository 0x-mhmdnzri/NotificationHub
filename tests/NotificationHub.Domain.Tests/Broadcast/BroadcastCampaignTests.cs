using FluentAssertions;
using NotificationHub.Domain.Broadcast;
using NotificationHub.Domain.Broadcast.Events;
using NotificationHub.Domain.Broadcast.ValueObjects;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery.ValueObjects;

namespace NotificationHub.Domain.Tests.Broadcast;

public class BroadcastCampaignTests
{
    private static BroadcastCampaign Draft() =>
        BroadcastCampaign.Create(
            CampaignId.New(),
            "Promo",
            TemplateKey.Create("promo"),
            [ChannelCode.Create("email"), ChannelCode.Create("sms")],
            null, null, null, "admin",
            DateTimeOffset.UtcNow);

    [Fact]
    public void TC_DDD_C01_Create_Raises_CampaignCreated()
    {
        var c = Draft();
        c.Status.Should().Be(CampaignStatus.Draft);
        c.DomainEvents.Should().Contain(e => e is CampaignCreated);
        c.CanAcceptRecipients.Should().BeTrue();
    }

    [Fact]
    public void TC_DDD_C02_Start_Moves_To_Processing()
    {
        var c = Draft();
        c.Start(DateTimeOffset.UtcNow);
        c.Status.Should().Be(CampaignStatus.Processing);
        c.StartedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void TC_DDD_C03_Cancel_From_Processing()
    {
        var c = Draft();
        c.Start(DateTimeOffset.UtcNow);
        c.Cancel(DateTimeOffset.UtcNow);
        c.Status.Should().Be(CampaignStatus.Cancelled);
    }

    [Fact]
    public void TC_DDD_C04_Completion_Partial()
    {
        var c = Draft();
        c.Start(DateTimeOffset.UtcNow);
        c.CompleteWithCounts(100, 90, 10, 0, 0, DateTimeOffset.UtcNow);
        c.Status.Should().Be(CampaignStatus.PartiallyCompleted);
        c.DomainEvents.Should().Contain(e => e is CampaignCompleted);
    }

    [Fact]
    public void TC_DDD_C05_No_Channels_Rejected()
    {
        var act = () => BroadcastCampaign.Create(
            CampaignId.New(), "X", TemplateKey.Create("t"),
            Array.Empty<ChannelCode>(), null, null, null, null, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void TC_DDD_C06_Illegal_Transition_Throws()
    {
        var c = Draft();
        c.Start(DateTimeOffset.UtcNow);
        c.CompleteWithCounts(1, 1, 0, 0, 0, DateTimeOffset.UtcNow);
        var act = () => c.Start(DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>();
    }
}
