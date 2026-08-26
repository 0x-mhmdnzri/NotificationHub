using NotificationHub.Abstractions.Models;
using MediatR;
using NotificationHub.Application.Common.Models;
using NotificationHub.Core.Store;

namespace NotificationHub.Application.Notifications.Queries.GetNotificationStatus;

public sealed class GetNotificationStatusQueryHandler(INotificationStatusStore store)
    : IRequestHandler<GetNotificationStatusQuery, Result<NotificationStatus>>
{
    public async Task<Result<NotificationStatus>> Handle(GetNotificationStatusQuery request, CancellationToken cancellationToken)
    {
        var status = await store.GetAsync(request.NotificationId, cancellationToken);
        if (status is null)
            return Result<NotificationStatus>.Failure("Not found", "NOT_FOUND", 404);

        if (!request.IsAdmin && request.TenantId is not null && status.TenantId != request.TenantId)
            return Result<NotificationStatus>.Failure("Not found", "NOT_FOUND", 404);

        return Result<NotificationStatus>.Success(status);
    }
}
