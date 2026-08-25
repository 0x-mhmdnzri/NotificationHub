using FluentAssertions;
using NotificationHub.Core.Observability;

namespace NotificationHub.Core.Tests.Observability;

public class MetricsServiceTests
{
    [Fact]
    public void TC_F_MET_001_IncrementAndObserve()
    {
        var m = new InMemoryMetricsService();
        m.Increment("requests", 1, ("channel", "email"));
        m.Increment("requests", 2, ("channel", "email"));
        m.Observe("latency_ms", 10, ("step", "send"));
        m.Observe("latency_ms", 30, ("step", "send"));
        var snap = m.Snapshot();
        snap.Counters["requests{channel=email}"].Should().Be(3);
        snap.Observations["latency_ms{step=send}"].Avg.Should().Be(20);
        snap.Observations["latency_ms{step=send}"].Max.Should().Be(30);
    }
}
