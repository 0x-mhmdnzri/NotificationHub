using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Workflow;

namespace NotificationHub.Application.Features.Workflows.GetRun;

public sealed class GetWorkflowRunHandler(IWorkflowEngine engine)
    : IRequestHandler<GetWorkflowRunQuery, Result<WorkflowRunStatusDto>>
{
    public async Task<Result<WorkflowRunStatusDto>> Handle(GetWorkflowRunQuery request, CancellationToken cancellationToken)
    {
        var run = await engine.GetRunAsync(request.RunId, cancellationToken);
        return run is null
            ? Result.Failure<WorkflowRunStatusDto>(Error.NotFound("workflow_run.not_found", "Workflow run not found."))
            : Result.Success(run);
    }
}
