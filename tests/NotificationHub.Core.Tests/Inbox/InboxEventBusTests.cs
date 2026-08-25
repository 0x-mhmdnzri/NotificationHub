using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Inbox;

namespace NotificationHub.Core.Tests.Inbox;

public class InboxEventBusTests
{
    [Fact]
    public async Task TC_F_BUS_001_Publish_DeliversToSubscriber()
    {
        var bus = new InMemoryInboxEventBus();
        var item = new InboxItem { Id = Guid.NewGuid(), UserId = "u1", Title = "T", Body = "B" };
        var received = new List<InboxItem>();
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var read = Task.Run(async () =>
        {
            await foreach (var x in bus.SubscribeAsync("u1", null, cts.Token))
            {
                received.Add(x);
                break;
            }
        });
        await Task.Delay(50);
        await bus.PublishAsync(item);
        await read;
        received.Should().ContainSingle(x => x.Title == "T");
    }
}
