using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Topics;

public interface ITopicService
{
    Task<TopicDefinition> SaveTopicAsync(TopicDefinition topic, CancellationToken ct = default);
    Task SubscribeAsync(string topicKey, string subscriberId, string? tenantId, string? channel, string? address, CancellationToken ct = default);
    Task UnsubscribeAsync(string topicKey, string subscriberId, string? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<TopicSubscriber>> ListSubscribersAsync(string topicKey, string? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<TopicDefinition>> ListTopicsAsync(string? tenantId, CancellationToken ct = default);
}
