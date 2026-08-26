using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Workflows.GetRun;

public sealed record GetWorkflowRunQuery(Guid RunId) : IQuery<Result<WorkflowRunStatusDto>>;
