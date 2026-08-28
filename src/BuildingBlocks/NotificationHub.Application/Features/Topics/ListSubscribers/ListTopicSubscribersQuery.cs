using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Topics.ListSubscribers;

public sealed record ListTopicSubscribersQuery(string TopicKey, string? TrustedTenantId)
    : IQuery<Result<IReadOnlyList<TopicSubscriber>>>;
