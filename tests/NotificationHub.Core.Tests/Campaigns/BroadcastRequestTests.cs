using FluentAssertions;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Tests.Campaigns;

public class BroadcastRequestTests
{
    [Fact]
    public void TC_F_BC_001_ModelDefaults()
    {
        var r = new BroadcastRequest
        {
            Name = "launch",
            Channel = "email",
            TemplateKey = "announce",
            Recipients = ["a@b.com", "c@d.com"]
        };
        r.Recipients.Should().HaveCount(2);
        r.Locale.Should().Be("en");
    }
}
