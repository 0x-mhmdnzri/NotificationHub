using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Engagement.Track;

public sealed record TrackEngagementCommand(EngagementEvent Event)
    : ICommand<Result<EngagementEvent>>;
