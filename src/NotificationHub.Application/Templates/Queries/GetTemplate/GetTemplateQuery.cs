using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Common.Interfaces;
using NotificationHub.Application.Common.Models;

namespace NotificationHub.Application.Templates.Queries.GetTemplate;

public sealed record GetTemplateQuery(
    string Key,
    string Channel,
    string? Locale,
    string? TenantId
) : IQuery<Result<TemplateDefinition>>;
