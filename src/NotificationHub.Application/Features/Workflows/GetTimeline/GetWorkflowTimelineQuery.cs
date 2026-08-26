using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Workflows.GetTimeline;

public sealed record GetWorkflowTimelineQuery(Guid RunId)
    : IQuery<Result<IReadOnlyList<WorkflowTimelineEventDto>>>;
