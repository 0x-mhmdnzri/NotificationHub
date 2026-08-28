using FluentAssertions;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery.ValueObjects;
using NotificationHub.Domain.Templates;

namespace NotificationHub.Domain.Tests.Templates;

public class NotificationTemplateTests
{
    [Fact]
    public void TC_DDD_T01_Update_increments_version()
    {
        var t = NotificationTemplate.Create(
            TemplateId.New(), TemplateKey.Create("welcome"), "email",
            "Hi", "Body", null, "en", null, DateTimeOffset.UtcNow);
        t.Version.Should().Be(1);
        t.UpdateContent("Hi2", "Body2", null, DateTimeOffset.UtcNow);
        t.Version.Should().Be(2);
        t.Subject.Should().Be("Hi2");
    }

    [Fact]
    public void TC_DDD_T02_Empty_subject_rejected()
    {
        var act = () => NotificationTemplate.Create(
            TemplateId.New(), TemplateKey.Create("x"), "email",
            " ", "body", null, "en", null, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>();
    }
}
