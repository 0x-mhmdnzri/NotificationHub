using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Templates.Delete;

[AuthorizeRoles(AppRoles.Admin)]
public sealed record DeleteTemplateCommand(
    string Key, string Channel, string Locale, string? TrustedTenantId
) : ICommand<Result>;
