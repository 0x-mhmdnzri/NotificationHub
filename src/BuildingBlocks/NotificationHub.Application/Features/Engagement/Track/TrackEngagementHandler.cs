using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Engagement;

namespace NotificationHub.Application.Features.Engagement.Track;

public sealed class TrackEngagementHandler(IEngagementService engagement)
    : IRequestHandler<TrackEngagementCommand, Result<EngagementEvent>>
{
    public async Task<Result<EngagementEvent>> Handle(TrackEngagementCommand request, CancellationToken cancellationToken)
    {
        var evt = await engagement.TrackAsync(request.Event, requireExistingNotification: true, cancellationToken);
        return evt is null
            ? Result.Failure<EngagementEvent>(Errors.NotificationNotFound)
            : Result.Success(evt);
    }
}
