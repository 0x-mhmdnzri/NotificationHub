using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Common.Models;
using NotificationHub.Core.Templates;

namespace NotificationHub.Application.Templates.Queries.GetTemplate;

public sealed class GetTemplateQueryHandler(ITemplateStore store)
    : IRequestHandler<GetTemplateQuery, Result<TemplateDefinition>>
{
    public async Task<Result<TemplateDefinition>> Handle(GetTemplateQuery request, CancellationToken cancellationToken)
    {
        var t = await store.FindAsync(request.Key, request.Channel, request.Locale ?? "en", request.TenantId, cancellationToken);
        return t is null
            ? Result<TemplateDefinition>.Failure("Template not found", "NOT_FOUND", 404)
            : Result<TemplateDefinition>.Success(t);
    }
}
