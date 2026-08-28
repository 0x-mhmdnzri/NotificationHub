using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Workflows.Save;

[AuthorizeRoles(AppRoles.Admin)]
public sealed record SaveWorkflowCommand(WorkflowDefinition Definition, string? TrustedTenantId)
    : ICommand<Result<WorkflowDefinition>>;
