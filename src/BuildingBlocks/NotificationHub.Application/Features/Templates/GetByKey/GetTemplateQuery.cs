using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Templates.GetByKey;

public sealed record GetTemplateQuery(
    string Key,
    string Channel,
    string Locale,
    string? TrustedTenantId
) : IQuery<Result<TemplateDto>>;

public sealed record TemplateDto(
    string Key,
    string Channel,
    string Locale,
    string Subject,
    string Body,
    string? HtmlBody,
    int Version,
    bool IsActive,
    string? TenantId
);
