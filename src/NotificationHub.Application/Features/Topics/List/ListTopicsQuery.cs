using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Topics.List;

public sealed record ListTopicsQuery(string? TrustedTenantId)
    : IQuery<Result<IReadOnlyList<TopicDefinition>>>;
