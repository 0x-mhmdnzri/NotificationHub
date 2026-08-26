using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Topics.Unsubscribe;

public sealed record UnsubscribeTopicCommand(string TopicKey, string SubscriberId, string? TrustedTenantId)
    : ICommand<Result>;
