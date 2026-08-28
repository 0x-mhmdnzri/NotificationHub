using FluentAssertions;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery;
using NotificationHub.Domain.Delivery.Events;
using NotificationHub.Domain.Delivery.ValueObjects;

namespace NotificationHub.Domain.Tests.Delivery;

public class NotificationTests
{
    private static Notification CreateQueued() =>
        Notification.Accept(
            NotificationId.New(),
            RecipientAddress.Create("user@example.com"),
            ChannelCode.Create("email"),
            TemplateKey.Create("welcome"),
            NotificationPriority.Normal,
            null, null, null, "en", null, null, null, true, null, null,
            DateTimeOffset.UtcNow);

    [Fact]
    public void TC_DDD_N01_Accept_Raises_NotificationAccepted()
    {
        var n = CreateQueued();
        n.Status.Should().Be(DeliveryStatus.Queued);
        n.DomainEvents.Should().ContainSingle(e => e is NotificationAccepted);
    }

    [Fact]
    public void TC_DDD_N02_Cannot_MarkSent_From_Queued()
    {
        var n = CreateQueued();
        var act = () => n.MarkSent("smtp", "m1", DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void TC_DDD_N03_Processing_Then_Sent()
    {
        var n = CreateQueued();
        n.MarkProcessing(DateTimeOffset.UtcNow);
        n.MarkSent("smtp", "m1", DateTimeOffset.UtcNow);
        n.Status.Should().Be(DeliveryStatus.Sent);
        n.ProviderId.Should().Be("smtp");
    }

    [Fact]
    public void TC_DDD_N04_Failed_Then_DeadLetter_On_MaxAttempts()
    {
        var n = CreateQueued();
        n.MarkProcessing(DateTimeOffset.UtcNow);
        n.MarkFailed("X", "err", maxAttempts: 1, DateTimeOffset.UtcNow);
        n.Status.Should().Be(DeliveryStatus.DeadLetter);
        n.DomainEvents.Should().Contain(e => e is NotificationDeadLettered);
    }

    [Fact]
    public void TC_DDD_N05_Empty_Recipient_Rejected()
    {
        var act = () => RecipientAddress.Create("  ");
        act.Should().Throw<DomainException>();
    }
}
