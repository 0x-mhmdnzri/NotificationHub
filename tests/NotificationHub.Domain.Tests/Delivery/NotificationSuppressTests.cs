using FluentAssertions;
using NotificationHub.Domain.Delivery;
using NotificationHub.Domain.Delivery.Events;
using NotificationHub.Domain.Delivery.ValueObjects;

namespace NotificationHub.Domain.Tests.Delivery;

public class NotificationSuppressTests
{
    [Fact]
    public void TC_DDD_S01_Preference_suppress_raises_domain_event()
    {
        var now = DateTimeOffset.UtcNow;
        var n = Notification.Accept(
            NotificationId.New(),
            RecipientAddress.Create("user@example.com"),
            ChannelCode.Create("email"),
            TemplateKey.Create("welcome"),
            NotificationPriority.Normal,
            null, null, null, "en", "marketing", null, null, true, null, null, now);

        n.Suppress("preference_denied", now);

        n.Status.Should().Be(DeliveryStatus.Suppressed);
        n.DomainEvents.Should().Contain(e => e is NotificationAccepted);
        n.DomainEvents.Should().Contain(e => e is NotificationSuppressed);
        var sup = n.DomainEvents.OfType<NotificationSuppressed>().Single();
        sup.Reason.Should().Be("preference_denied");
        sup.Recipient.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void TC_DDD_S02_Cannot_suppress_from_sent()
    {
        var now = DateTimeOffset.UtcNow;
        var n = Notification.Accept(
            NotificationId.New(),
            RecipientAddress.Create("u@x.com"),
            ChannelCode.Create("sms"),
            TemplateKey.Create("otp"),
            NotificationPriority.Critical,
            null, null, null, null, null, null, null, true, null, null, now);
        n.MarkProcessing(now);
        n.MarkSent("twilio", "sid", now);

        var act = () => n.Suppress("too_late", now);
        act.Should().Throw<Exception>();
    }
}
