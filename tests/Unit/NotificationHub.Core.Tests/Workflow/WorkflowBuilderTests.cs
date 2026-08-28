using FluentAssertions;
using NotificationHub.Core.Workflow;

namespace NotificationHub.Core.Tests.Workflow;

public class WorkflowBuilderTests
{
    [Fact]
    public void TC_F_WFB_001_Build_ValidatesAndChains()
    {
        var def = WorkflowBuilder.Create("onboarding")
            .Send("s1", "email", "welcome", "d1")
            .Delay("d1", 3600, "s2")
            .Send("s2", "email", "tips", null)
            .Build();
        def.Key.Should().Be("onboarding");
        def.Steps.Should().HaveCount(3);
        def.Steps[0].Type.Should().Be("send");
    }

    [Fact]
    public void TC_ERR_WFB_002_EmptySteps_Throws()
    {
        var act = () => WorkflowBuilder.Create("x").Build();
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void TC_F_WFB_003_HttpStep_Included()
    {
        var def = WorkflowBuilder.Create("enrich")
            .Http("h1", "https://example.com/api", "GET", "s1")
            .Send("s1", "email", "t", null)
            .Build();
        def.Steps[0].Type.Should().Be("http");
        def.Steps[0].ConfigJson.Should().Contain("example.com");
    }
}
