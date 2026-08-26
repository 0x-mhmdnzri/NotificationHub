using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Workflows.Start;

public sealed record StartWorkflowCommand(WorkflowStartRequest Request, string? TrustedTenantId)
    : ICommand<Result<Guid>>;
