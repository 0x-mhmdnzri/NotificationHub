using MediatR;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Application.Features.Templates.GetByKey;

public sealed class GetTemplateHandler(NotificationDbContext db)
    : IRequestHandler<GetTemplateQuery, Result<TemplateDto>>
{
    public async Task<Result<TemplateDto>> Handle(GetTemplateQuery request, CancellationToken cancellationToken)
    {
        var q = db.Templates.AsNoTracking()
            .Where(x => x.Key == request.Key && x.Channel == request.Channel && x.Locale == request.Locale);

        q = request.TrustedTenantId is null
            ? q.Where(x => x.TenantId == null)
            : q.Where(x => x.TenantId == request.TrustedTenantId);

        var dto = await q.Select(x => new TemplateDto(
                x.Key, x.Channel, x.Locale, x.Subject, x.Body, x.HtmlBody, x.Version, x.IsActive, x.TenantId))
            .FirstOrDefaultAsync(cancellationToken);

        return dto is null
            ? Result.Failure<TemplateDto>(Errors.TemplateNotFound)
            : Result.Success(dto);
    }
}
