using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Messaging;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Messaging;

public class OutboxInboxTests
{
    [Fact]
    public async Task TC_MSG_001_Outbox_Add_PersistsPending()
    {
        await using var db = TestFixtures.CreateDbContext();
        var outbox = new EfOutbox(db);
        var id = Guid.NewGuid();
        await outbox.AddAsync(new NotificationRequest
        {
            Id = id, Recipient = "a@b.com", Channel = "email", TemplateKey = "welcome", Data = new()
        });
        await db.SaveChangesAsync();
        db.OutboxMessages.Should().ContainSingle(x => x.NotificationId == id && x.Status == "pending");
    }

    [Fact]
    public async Task TC_MSG_002_Inbox_SecondCall_IsDuplicate()
    {
        await using var db = TestFixtures.CreateDbContext();
        var inbox = new EfInbox(db);
        (await inbox.TryMarkProcessedAsync("msg-1")).Should().BeTrue();
        (await inbox.TryMarkProcessedAsync("msg-1")).Should().BeFalse();
    }

    [Fact]
    public async Task TC_MSG_003_Inbox_Exists_BeforeAndAfterMark()
    {
        await using var db = TestFixtures.CreateDbContext();
        var inbox = new EfInbox(db);
        (await inbox.ExistsAsync("x")).Should().BeFalse();
        await inbox.TryMarkProcessedAsync("x");
        (await inbox.ExistsAsync("x")).Should().BeTrue();
    }
}
