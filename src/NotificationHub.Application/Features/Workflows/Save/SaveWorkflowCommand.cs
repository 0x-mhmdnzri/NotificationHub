using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Workflows.Save;

public sealed record SaveWorkflowCommand(WorkflowDefinition Definition, string? TrustedTenantId)
    : ICommand<Result<WorkflowDefinition>>;
