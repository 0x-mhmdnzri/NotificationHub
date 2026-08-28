using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Workflow;

namespace NotificationHub.Application.Features.Workflows.Cancel;

public sealed class CancelWorkflowHandler(IWorkflowEngine engine)
    : IRequestHandler<CancelWorkflowCommand, Result>
{
    public async Task<Result> Handle(CancelWorkflowCommand request, CancellationToken cancellationToken)
    {
        var ok = await engine.CancelAsync(request.RunId, cancellationToken);
        return ok ? Result.Success() : Result.Failure(Error.NotFound("workflow_run.not_found", "Workflow run not found."));
    }
}
