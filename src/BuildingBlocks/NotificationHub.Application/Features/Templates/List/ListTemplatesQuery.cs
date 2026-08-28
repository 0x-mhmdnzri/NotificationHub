using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Templates.List;

public sealed record ListTemplatesQuery(string? TrustedTenantId, string? Channel)
    : IQuery<Result<IReadOnlyList<TemplateListItemDto>>>;

public sealed record TemplateListItemDto(
    string Key,
    string Channel,
    string Locale,
    string Subject,
    int Version,
    bool IsActive,
    string? TenantId
);
