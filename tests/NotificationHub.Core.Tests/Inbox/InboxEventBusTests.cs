using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Inbox;

namespace NotificationHub.Core.Tests.Inbox;

public class InboxEventBusTests
{
    [Fact]
    public async Task TC_F_BUS_001_Publish_DeliversToSubscriber()
    {
        var userId = "u-" + Guid.NewGuid().ToString("N");
        var bus = new InMemoryInboxEventBus();
        var item = new InboxItem { Id = Guid.NewGuid(), UserId = userId, Title = "T", Body = "B" };
        var received = new List<InboxItem>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var read = Task.Run(async () =>
        {
            await foreach (var x in bus.SubscribeAsync(userId, null, cts.Token))
            {
                received.Add(x);
                break;
            }
        }, cts.Token);

        // Give the subscriber a chance to register; Publish uses GetOrAdd so early publish still buffers
        await Task.Delay(20);
        await bus.PublishAsync(item);
        await read.WaitAsync(cts.Token);

        received.Should().ContainSingle(x => x.Title == "T" && x.UserId == userId);
    }

    [Fact]
    public async Task TC_F_BUS_002_Publish_BeforeSubscribe_StillBuffered()
    {
        var userId = "u-" + Guid.NewGuid().ToString("N");
        var bus = new InMemoryInboxEventBus();
        var item = new InboxItem { Id = Guid.NewGuid(), UserId = userId, Title = "Buffered", Body = "B" };

        await bus.PublishAsync(item);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        InboxItem? got = null;
        await foreach (var x in bus.SubscribeAsync(userId, null, cts.Token))
        {
            got = x;
            break;
        }

        got.Should().NotBeNull();
        got!.Title.Should().Be("Buffered");
    }
}
