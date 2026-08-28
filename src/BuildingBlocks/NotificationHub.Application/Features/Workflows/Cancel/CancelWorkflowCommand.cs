using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Workflows.Cancel;

[AuthorizeRoles(AppRoles.Admin)]
public sealed record CancelWorkflowCommand(Guid RunId) : ICommand<Result>;
