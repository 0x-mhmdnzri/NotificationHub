using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Topics.Save;

public sealed record SaveTopicCommand(TopicDefinition Topic, string? TrustedTenantId)
    : ICommand<Result<TopicDefinition>>;
