using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Templates;

namespace NotificationHub.Application.Features.Templates.Save;

public sealed class SaveTemplateHandler(ITemplateEngine engine)
    : IRequestHandler<SaveTemplateCommand, Result<TemplateDefinition>>
{
    public async Task<Result<TemplateDefinition>> Handle(SaveTemplateCommand request, CancellationToken cancellationToken)
    {
        await engine.RegisterTemplateAsync(request.Template, cancellationToken);
        return Result.Success(request.Template);
    }
}
