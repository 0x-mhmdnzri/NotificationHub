using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Common.Interfaces;

namespace NotificationHub.Application.Templates.Queries.ListTemplates;

public sealed record ListTemplatesQuery(string? TenantId, string? Channel)
    : IQuery<IReadOnlyList<TemplateDefinition>>;
