using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Common.Interfaces;

namespace NotificationHub.Application.Templates.Commands.SaveTemplate;

public sealed record SaveTemplateCommand(TemplateDefinition Template) : ICommand<TemplateDefinition>;
