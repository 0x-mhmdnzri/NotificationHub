using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Segmentation;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Segmentation;

public class SegmentServiceTests
{
    [Fact]
    public async Task TC_F_050_Segment_MatchAll_Rules()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new SegmentService(db);
        await sut.SaveAsync(new SegmentDefinition
        {
            Key = "vip",
            MatchAll = true,
            Rules =
            [
                new SegmentRule { Field = "plan", Operator = "eq", Value = "pro" },
                new SegmentRule { Field = "country", Operator = "eq", Value = "IR" }
            ]
        });

        var ok = await sut.MatchesAsync("vip", new Dictionary<string, object?> { ["plan"] = "pro", ["country"] = "IR" });
        var no = await sut.MatchesAsync("vip", new Dictionary<string, object?> { ["plan"] = "pro", ["country"] = "US" });
        ok.Should().BeTrue();
        no.Should().BeFalse();
    }
}
