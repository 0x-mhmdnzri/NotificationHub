using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Orchestration;

namespace NotificationHub.Core.Tests.Orchestration;

public class BroadcastStateMachineTests
{
    [Theory]
    [InlineData(CampaignStatus.Draft, CampaignStatus.Preparing, true)]
    [InlineData(CampaignStatus.Draft, CampaignStatus.Completed, false)]
    [InlineData(CampaignStatus.Delivering, CampaignStatus.PartiallyCompleted, true)]
    [InlineData(CampaignStatus.Completed, CampaignStatus.Processing, false)]
    [InlineData(CampaignStatus.Cancelled, CampaignStatus.Draft, false)]
    public void TC_ORCH_001_Transitions(CampaignStatus from, CampaignStatus to, bool allowed)
        => BroadcastStateMachine.CanTransition(from, to).Should().Be(allowed);

    [Fact]
    public void TC_ORCH_002_Completion_Partial()
        => BroadcastStateMachine.ResolveCompletion(100, 90, 10, 0, 0)
            .Should().Be(CampaignStatus.PartiallyCompleted);

    [Fact]
    public void TC_ORCH_003_Completion_AllFailed()
        => BroadcastStateMachine.ResolveCompletion(10, 0, 10, 0, 0)
            .Should().Be(CampaignStatus.Failed);

    [Fact]
    public void TC_ORCH_004_Completion_Success()
        => BroadcastStateMachine.ResolveCompletion(5, 5, 0, 0, 0)
            .Should().Be(CampaignStatus.Completed);

    [Fact]
    public void TC_ORCH_005_Ensure_Throws()
    {
        var act = () => BroadcastStateMachine.EnsureTransition(CampaignStatus.Completed, CampaignStatus.Draft);
        act.Should().Throw<InvalidOperationException>();
    }
}
