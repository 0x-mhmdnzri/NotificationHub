using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Workflow;

namespace NotificationHub.Application.Features.Workflows.Start;

public sealed class StartWorkflowHandler(IWorkflowEngine engine)
    : IRequestHandler<StartWorkflowCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(StartWorkflowCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        if (!string.IsNullOrEmpty(request.TrustedTenantId))
            req = req with { TenantId = request.TrustedTenantId };
        try
        {
            var id = await engine.StartAsync(req, cancellationToken);
            return Result.Success(id);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<Guid>(Error.NotFound("workflow.not_found", ex.Message));
        }
    }
}
