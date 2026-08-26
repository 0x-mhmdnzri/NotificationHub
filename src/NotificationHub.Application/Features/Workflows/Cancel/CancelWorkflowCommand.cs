using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Workflows.Cancel;

public sealed record CancelWorkflowCommand(Guid RunId) : ICommand<Result>;
