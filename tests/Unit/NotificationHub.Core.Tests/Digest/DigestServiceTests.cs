using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Digest;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Digest;

/// <summary>F02 — Digest buffer + flush.</summary>
public class DigestServiceTests
{
    [Fact]
    public async Task TC_F_DIGEST_001_SavePolicy_AndBuffer()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new DigestService(db, NullLogger<DigestService>.Instance);
        await sut.SavePolicyAsync(new DigestPolicy { Key = "comments", WindowMinutes = 1, Channel = "email" });
        await sut.BufferAsync("comments", "a@b.com", null, new { text = "c1" });
        await sut.BufferAsync("comments", "a@b.com", null, new { text = "c2" });
        db.DigestBuffers.Count(x => x.FlushedAt == null).Should().Be(2);
    }

    [Fact]
    public async Task TC_F_DIGEST_002_FlushDue_MarksOldRows()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new DigestService(db, NullLogger<DigestService>.Instance);
        await sut.SavePolicyAsync(new DigestPolicy { Key = "d", WindowMinutes = 1 });
        db.DigestBuffers.Add(new NotificationHub.Core.Persistence.DigestBufferEntity
        {
            PolicyKey = "d",
            Recipient = "u",
            PayloadJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();

        var n = await sut.FlushDueAsync();
        n.Should().Be(1);
        db.DigestBuffers.Count(x => x.FlushedAt != null).Should().Be(1);
    }

    [Fact]
    public async Task TC_E_DIGEST_003_Flush_SkipsFreshRows()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new DigestService(db, NullLogger<DigestService>.Instance);
        await sut.SavePolicyAsync(new DigestPolicy { Key = "d", WindowMinutes = 60 });
        await sut.BufferAsync("d", "u", null, new { });
        (await sut.FlushDueAsync()).Should().Be(0);
    }
}
