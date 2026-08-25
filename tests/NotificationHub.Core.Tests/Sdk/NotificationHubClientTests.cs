using FluentAssertions;
using NotificationHub.Sdk;

namespace NotificationHub.Core.Tests.Sdk;

public class NotificationHubClientTests
{
    [Fact]
    public void TC_F_SDK_001_ConstructsWithApiKeyHeader()
    {
        using var client = new NotificationHubClient("http://localhost:5000", "test-key");
        client.Should().NotBeNull();
    }
}
