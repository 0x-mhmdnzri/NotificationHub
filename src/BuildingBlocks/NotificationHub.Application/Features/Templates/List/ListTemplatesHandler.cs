using MediatR;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Application.Features.Templates.List;

public sealed class ListTemplatesHandler(NotificationDbContext db)
    : IRequestHandler<ListTemplatesQuery, Result<PagedResult<TemplateListItemDto>>>
{
    public async Task<Result<PagedResult<TemplateListItemDto>>> Handle(
        ListTemplatesQuery request,
        CancellationToken cancellationToken)
    {
        var page = request.Page ?? new PagedRequest();
        var q = db.Templates.AsNoTracking().AsQueryable();

        if (request.TrustedTenantId is not null)
            q = q.Where(x => x.TenantId == request.TrustedTenantId);
        if (!string.IsNullOrEmpty(request.Channel))
            q = q.Where(x => x.Channel == request.Channel);
        if (!string.IsNullOrWhiteSpace(page.Search))
        {
            var s = page.Search.Trim();
            q = q.Where(x =>
                x.Key.Contains(s) ||
                x.Subject.Contains(s) ||
                x.Locale.Contains(s));
        }

        var total = await q.CountAsync(cancellationToken);

        q = (page.Sort?.ToLowerInvariant()) switch
        {
            "channel" => page.Descending ? q.OrderByDescending(x => x.Channel) : q.OrderBy(x => x.Channel),
            "locale" => page.Descending ? q.OrderByDescending(x => x.Locale) : q.OrderBy(x => x.Locale),
            "version" => page.Descending ? q.OrderByDescending(x => x.Version) : q.OrderBy(x => x.Version),
            "active" or "isactive" => page.Descending ? q.OrderByDescending(x => x.IsActive) : q.OrderBy(x => x.IsActive),
            "subject" => page.Descending ? q.OrderByDescending(x => x.Subject) : q.OrderBy(x => x.Subject),
            _ => page.Descending ? q.OrderByDescending(x => x.Key) : q.OrderBy(x => x.Key),
        };

        var items = await q
            .Skip(page.Skip)
            .Take(page.SafePageSize)
            .Select(x => new TemplateListItemDto(x.Key, x.Channel, x.Locale, x.Subject, x.Version, x.IsActive, x.TenantId))
            .ToListAsync(cancellationToken);

        return Result.Success(new PagedResult<TemplateListItemDto>(items, page.SafePage, page.SafePageSize, total));
    }
}
