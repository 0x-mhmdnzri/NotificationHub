using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Templates.Save;

public sealed record SaveTemplateCommand(TemplateDefinition Template) : ICommand<Result<TemplateDefinition>>;
