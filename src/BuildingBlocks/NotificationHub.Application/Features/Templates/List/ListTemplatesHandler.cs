using MediatR;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Application.Features.Templates.List;

public sealed class ListTemplatesHandler(NotificationDbContext db)
    : IRequestHandler<ListTemplatesQuery, Result<IReadOnlyList<TemplateListItemDto>>>
{
    public async Task<Result<IReadOnlyList<TemplateListItemDto>>> Handle(
        ListTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var q = db.Templates.AsNoTracking().AsQueryable();

        if (request.TrustedTenantId is not null)
            q = q.Where(x => x.TenantId == request.TrustedTenantId);
        if (!string.IsNullOrEmpty(request.Channel))
            q = q.Where(x => x.Channel == request.Channel);

        var list = await q
            .OrderBy(x => x.Key)
            .Select(x => new TemplateListItemDto(x.Key, x.Channel, x.Locale, x.Subject, x.Version, x.IsActive, x.TenantId))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<TemplateListItemDto>>(list);
    }
}
