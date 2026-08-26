using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Topics.Subscribe;

public sealed record SubscribeTopicCommand(
    string TopicKey, string SubscriberId, string? TrustedTenantId, string? Channel, string? Address
) : ICommand<Result>;
