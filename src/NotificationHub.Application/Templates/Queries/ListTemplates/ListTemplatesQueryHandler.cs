using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Templates;

namespace NotificationHub.Application.Templates.Queries.ListTemplates;

public sealed class ListTemplatesQueryHandler(ITemplateStore store)
    : IRequestHandler<ListTemplatesQuery, IReadOnlyList<TemplateDefinition>>
{
    public Task<IReadOnlyList<TemplateDefinition>> Handle(ListTemplatesQuery request, CancellationToken cancellationToken)
        => store.ListAsync(request.TenantId, request.Channel, cancellationToken);
}
