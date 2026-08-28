using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Engagement;

namespace NotificationHub.Application.Features.Engagement.ListByNotification;

public sealed class ListEngagementHandler(IEngagementService engagement)
    : IRequestHandler<ListEngagementQuery, Result<IReadOnlyList<EngagementEvent>>>
{
    public async Task<Result<IReadOnlyList<EngagementEvent>>> Handle(ListEngagementQuery request, CancellationToken cancellationToken)
    {
        var list = await engagement.ListByNotificationAsync(request.NotificationId, cancellationToken);
        return Result.Success(list);
    }
}
