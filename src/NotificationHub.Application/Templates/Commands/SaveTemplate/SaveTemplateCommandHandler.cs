using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Templates;

namespace NotificationHub.Application.Templates.Commands.SaveTemplate;

public sealed class SaveTemplateCommandHandler(ITemplateEngine engine)
    : IRequestHandler<SaveTemplateCommand, TemplateDefinition>
{
    public async Task<TemplateDefinition> Handle(SaveTemplateCommand request, CancellationToken cancellationToken)
    {
        await engine.RegisterTemplateAsync(request.Template, cancellationToken);
        return request.Template;
    }
}
