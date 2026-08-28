using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Engagement.ListByNotification;

public sealed record ListEngagementQuery(Guid NotificationId)
    : IQuery<Result<IReadOnlyList<EngagementEvent>>>;
