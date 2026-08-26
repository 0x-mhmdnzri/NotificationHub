using MediatR;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Application.Features.Notifications.GetStatus;

/// <summary>
/// Read path: projected DTO, AsNoTracking, no domain aggregate load.
/// Tenant isolation from trusted context — not solely client input.
/// </summary>
public sealed class GetNotificationStatusHandler(NotificationDbContext db)
    : IRequestHandler<GetNotificationStatusQuery, Result<NotificationStatusDto>>
{
    public async Task<Result<NotificationStatusDto>> Handle(
        GetNotificationStatusQuery request,
        CancellationToken cancellationToken)
    {
        var dto = await db.NotificationStatuses
            .AsNoTracking()
            .Where(x => x.Id == request.NotificationId)
            .Select(x => new NotificationStatusDto(
                x.Id,
                x.Channel,
                x.Recipient,
                x.Status.ToString(),
                x.ProviderId,
                x.ErrorCode,
                x.ErrorMessage,
                x.AttemptCount,
                x.CreatedAt,
                x.UpdatedAt,
                x.TenantId,
                x.CorrelationId,
                x.Category))
            .SingleOrDefaultAsync(cancellationToken);

        if (dto is null)
            return Result.Failure<NotificationStatusDto>(Errors.NotificationNotFound);

        if (!request.IsAdmin &&
            request.TrustedTenantId is not null &&
            dto.TenantId != request.TrustedTenantId)
            return Result.Failure<NotificationStatusDto>(Errors.NotificationNotFound); // no existence leak

        return Result.Success(dto);
    }
}
