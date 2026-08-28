using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Workflow;

namespace NotificationHub.Core.Tests.Workflow;

/// <summary>F07 — workflow DSL export/import/validate.</summary>
public class WorkflowDslTests
{
    [Fact]
    public void TC_F_WF_001_ExportImport_RoundTrip()
    {
        var def = new WorkflowDefinition
        {
            Key = "welcome",
            Steps = [new WorkflowStep { Id = "s1", Type = "send", Channel = "email", TemplateKey = "hi", Next = null }]
        };
        var json = WorkflowDsl.Export(def);
        var doc = WorkflowDsl.Import(json);
        doc.Definition.Key.Should().Be("welcome");
        doc.Definition.Steps.Should().HaveCount(1);
    }

    [Fact]
    public void TC_ERR_WF_002_UnknownStepType_Throws()
    {
        var def = new WorkflowDefinition
        {
            Key = "bad",
            Steps = [new WorkflowStep { Id = "s1", Type = "magic" }]
        };
        var act = () => WorkflowDsl.Validate(def);
        act.Should().Throw<ArgumentException>().WithMessage("*Unknown*");
    }

    [Fact]
    public void TC_ERR_WF_003_DuplicateStepId_Throws()
    {
        var def = new WorkflowDefinition
        {
            Key = "bad",
            Steps =
            [
                new WorkflowStep { Id = "s1", Type = "delay", DelaySeconds = 1 },
                new WorkflowStep { Id = "s1", Type = "send" }
            ]
        };
        var act = () => WorkflowDsl.Validate(def);
        act.Should().Throw<ArgumentException>().WithMessage("*Duplicate*");
    }

    [Fact]
    public void TC_F_WF_004_HttpStep_IsKnown()
    {
        var def = new WorkflowDefinition
        {
            Key = "http-flow",
            Steps = [new WorkflowStep { Id = "h1", Type = "http", ConfigJson = """{"url":"https://example.com","method":"GET"}""" }]
        };
        var act = () => WorkflowDsl.Validate(def);
        act.Should().NotThrow();
    }
}
