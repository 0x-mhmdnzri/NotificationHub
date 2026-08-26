using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Workflow;

namespace NotificationHub.Application.Features.Workflows.GetTimeline;

public sealed class GetWorkflowTimelineHandler(IWorkflowEngine engine)
    : IRequestHandler<GetWorkflowTimelineQuery, Result<IReadOnlyList<WorkflowTimelineEventDto>>>
{
    public async Task<Result<IReadOnlyList<WorkflowTimelineEventDto>>> Handle(
        GetWorkflowTimelineQuery request, CancellationToken cancellationToken)
    {
        var events = await engine.GetTimelineAsync(request.RunId, cancellationToken);
        return Result.Success(events);
    }
}
