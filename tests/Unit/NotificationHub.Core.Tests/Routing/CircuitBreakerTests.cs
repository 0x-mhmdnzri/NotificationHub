using NotificationHub.Abstractions.Models;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NotificationHub.Core.Routing;

namespace NotificationHub.Core.Tests.Routing;

/// <summary>F19 — circuit breaker opens after consecutive failures.</summary>
public class CircuitBreakerTests
{
    [Fact]
    public void TC_F_CB_001_Opens_AfterThreshold()
    {
        var tracker = new CircuitBreakerProviderHealthTracker(
            Options.Create(new ProviderHealthOptions { WindowSize = 20, MinSamples = 1, UnhealthyThreshold = 0.5 }),
            Options.Create(new CircuitBreakerOptions { FailureThreshold = 3, OpenDurationSeconds = 60 }));

        for (var i = 0; i < 3; i++)
            tracker.RecordFailure("email-resend", "email", "fail");

        tracker.GetCircuitState("email-resend", "email").Should().Be(CircuitState.Open);
        tracker.GetHealth("email-resend", "email").IsHealthy.Should().BeFalse();
    }

    [Fact]
    public void TC_F_CB_002_Success_ClosesCircuit()
    {
        var tracker = new CircuitBreakerProviderHealthTracker(
            Options.Create(new ProviderHealthOptions()),
            Options.Create(new CircuitBreakerOptions { FailureThreshold = 2, OpenDurationSeconds = 60 }));

        tracker.RecordFailure("p", "sms", "x");
        tracker.RecordFailure("p", "sms", "x");
        tracker.GetCircuitState("p", "sms").Should().Be(CircuitState.Open);
        tracker.RecordSuccess("p", "sms");
        tracker.GetCircuitState("p", "sms").Should().Be(CircuitState.Closed);
    }

    [Fact]
    public void TC_F_CB_003_HalfOpen_AfterDuration()
    {
        var tracker = new CircuitBreakerProviderHealthTracker(
            Options.Create(new ProviderHealthOptions()),
            Options.Create(new CircuitBreakerOptions { FailureThreshold = 1, OpenDurationSeconds = 0 }));

        tracker.RecordFailure("p", "email", "x");
        // OpenDuration 0 → immediate half-open on next read
        tracker.GetCircuitState("p", "email").Should().Be(CircuitState.HalfOpen);
    }
}
