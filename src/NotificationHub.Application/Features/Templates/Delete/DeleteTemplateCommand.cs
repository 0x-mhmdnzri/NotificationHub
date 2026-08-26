using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Templates.Delete;

public sealed record DeleteTemplateCommand(
    string Key, string Channel, string Locale, string? TrustedTenantId
) : ICommand<Result>;
