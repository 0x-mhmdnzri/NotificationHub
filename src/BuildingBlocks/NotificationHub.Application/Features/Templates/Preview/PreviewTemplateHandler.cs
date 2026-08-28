using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Templates;

namespace NotificationHub.Application.Features.Templates.Preview;

public sealed class PreviewTemplateHandler(ITemplateEngine engine)
    : IRequestHandler<PreviewTemplateQuery, Result<RenderedNotification>>
{
    public async Task<Result<RenderedNotification>> Handle(PreviewTemplateQuery request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        if (!string.IsNullOrEmpty(request.TrustedTenantId))
            req = req with { TenantId = request.TrustedTenantId };
        var rendered = await engine.RenderAsync(req, cancellationToken);
        return Result.Success(rendered);
    }
}
