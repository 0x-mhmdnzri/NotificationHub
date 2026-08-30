using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Digest;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Digest;

public class DigestFlushEnqueueTests
{
    [Fact]
    public async Task TC_F_DIG_010_Flush_WithoutOrch_StillMarksFlushed()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new DigestService(db, NullLogger<DigestService>.Instance);
        await sut.SavePolicyAsync(new DigestPolicy { Key = "d", WindowMinutes = 1 });
        db.DigestBuffers.Add(new DigestBufferEntity
        {
            PolicyKey = "d",
            Recipient = "a@b.com",
            PayloadJson = "{}",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5)
        });
        await db.SaveChangesAsync();
        (await sut.FlushDueAsync()).Should().Be(1);
        db.DigestBuffers.Count(x => x.FlushedAt != null).Should().Be(1);
    }
}
