using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Expressions;
using NotificationHub.Core.Tests.Helpers;
using NotificationHub.Core.Workflow;
using NotificationHub.Core.Workflow.Handlers;

namespace NotificationHub.Core.Tests.Workflow;

public class WorkflowEngineTests
{
    private static WorkflowEngine CreateEngine(NotificationHub.Core.Persistence.NotificationDbContext db)
    {
        var repo = new WorkflowRunRepository(db);
        var timeline = new WorkflowTimeline(db);
        var evaluator = new SimpleExpressionEvaluator();
        var handlers = new IWorkflowStepHandler[]
        {
            new DelayStepHandler(),
            new ConditionStepHandler(evaluator),
            new BranchStepHandler(evaluator)
        };
        return new WorkflowEngine(repo, timeline, handlers, NullLogger<WorkflowEngine>.Instance);
    }

    [Fact]
    public async Task TC_F_070_Start_CreatesRunAndTimelineStarted()
    {
        await using var db = TestFixtures.CreateDbContext();
        var engine = CreateEngine(db);

        await engine.SaveAsync(new WorkflowDefinition
        {
            Key = "onboarding",
            Steps =
            [
                new WorkflowStep { Id = "d1", Type = "delay", DelaySeconds = 0, Next = "c1" },
                new WorkflowStep { Id = "c1", Type = "condition", ConditionExpression = "plan == \"pro\"", NextOnTrue = null, NextOnFalse = null }
            ]
        });

        var runId = await engine.StartAsync(new WorkflowStartRequest
        {
            WorkflowKey = "onboarding",
            Recipient = "u1",
            Data = new Dictionary<string, object?> { ["plan"] = "pro" }
        });

        var status = await engine.GetRunAsync(runId);
        status.Should().NotBeNull();
        status!.Status.Should().Be("running");

        var timeline = await engine.GetTimelineAsync(runId);
        timeline.Should().Contain(e => e.EventType == "started");
    }

    [Fact]
    public async Task TC_ST_020_Process_DelayAndCondition_CompletesWithTimeline()
    {
        await using var db = TestFixtures.CreateDbContext();
        var engine = CreateEngine(db);

        await engine.SaveAsync(new WorkflowDefinition
        {
            Key = "flow",
            Steps =
            [
                new WorkflowStep { Id = "d1", Type = "delay", DelaySeconds = 0, Next = "c1" },
                new WorkflowStep { Id = "c1", Type = "condition", ConditionExpression = "plan == \"pro\"", NextOnTrue = null, NextOnFalse = null }
            ]
        });

        var runId = await engine.StartAsync(new WorkflowStartRequest
        {
            WorkflowKey = "flow",
            Recipient = "u1",
            Data = new Dictionary<string, object?> { ["plan"] = "pro" }
        });

        await engine.ProcessDueRunsAsync(); // delay
        await engine.ProcessDueRunsAsync(); // condition -> complete

        var status = await engine.GetRunAsync(runId);
        status!.Status.Should().Be("completed");

        var timeline = await engine.GetTimelineAsync(runId);
        timeline.Select(x => x.EventType).Should().Contain(new[] { "started", "step_entered", "delayed", "step_completed", "branched", "completed" });
    }

    [Fact]
    public async Task TC_F_071_Cancel_RunningRun()
    {
        await using var db = TestFixtures.CreateDbContext();
        var engine = CreateEngine(db);
        await engine.SaveAsync(new WorkflowDefinition
        {
            Key = "long",
            Steps = [new WorkflowStep { Id = "d1", Type = "delay", DelaySeconds = 3600 }]
        });
        var runId = await engine.StartAsync(new WorkflowStartRequest { WorkflowKey = "long", Recipient = "u1", Data = new() });
        var ok = await engine.CancelAsync(runId);
        ok.Should().BeTrue();
        var status = await engine.GetRunAsync(runId);
        status!.Status.Should().Be("cancelled");
        var timeline = await engine.GetTimelineAsync(runId);
        timeline.Should().Contain(e => e.EventType == "cancelled");
    }
}
