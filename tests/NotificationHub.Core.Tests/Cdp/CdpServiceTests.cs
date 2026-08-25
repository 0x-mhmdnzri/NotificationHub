using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Cdp;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Cdp;

public class CdpServiceTests
{
    [Fact]
    public async Task TC_F_CDP_001_Identify_UpsertsProfile()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new CdpService(db, NullLogger<CdpService>.Instance);
        var p1 = await sut.IdentifyAsync(new CdpIdentifyRequest
        {
            UserId = "u1",
            Email = "a@b.com",
            Traits = new Dictionary<string, object?> { ["plan"] = "pro" }
        });
        p1.Email.Should().Be("a@b.com");
        var p2 = await sut.IdentifyAsync(new CdpIdentifyRequest
        {
            UserId = "u1",
            Traits = new Dictionary<string, object?> { ["seats"] = 5 }
        });
        p2.Traits.Should().ContainKey("plan");
        p2.Traits.Should().ContainKey("seats");
    }

    [Fact]
    public async Task TC_F_CDP_002_Track_PersistsEvent()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new CdpService(db, NullLogger<CdpService>.Instance);
        await sut.TrackAsync(new CdpTrackRequest { UserId = "u1", Event = "signed_up" });
        db.CdpEvents.Count().Should().Be(1);
        db.CdpEvents.First().EventName.Should().Be("signed_up");
    }
}
