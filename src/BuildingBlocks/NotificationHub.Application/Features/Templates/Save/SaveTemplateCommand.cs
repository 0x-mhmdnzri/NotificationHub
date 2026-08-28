using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Templates.Save;

[AuthorizeRoles(AppRoles.Admin, AppRoles.Sender)]
public sealed record SaveTemplateCommand(TemplateDefinition Template) : ICommand<Result<TemplateDefinition>>;
