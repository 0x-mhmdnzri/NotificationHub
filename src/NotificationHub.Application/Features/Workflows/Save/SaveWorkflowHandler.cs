using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Common;
using NotificationHub.Core.Workflow;

namespace NotificationHub.Application.Features.Workflows.Save;

public sealed class SaveWorkflowHandler(IWorkflowEngine engine)
    : IRequestHandler<SaveWorkflowCommand, Result<WorkflowDefinition>>
{
    public async Task<Result<WorkflowDefinition>> Handle(SaveWorkflowCommand request, CancellationToken cancellationToken)
    {
        var def = request.Definition with
        {
            Id = ServerIds.New(),
            TenantId = request.TrustedTenantId ?? request.Definition.TenantId
        };
        var saved = await engine.SaveAsync(def, cancellationToken);
        return Result.Success(saved);
    }
}
