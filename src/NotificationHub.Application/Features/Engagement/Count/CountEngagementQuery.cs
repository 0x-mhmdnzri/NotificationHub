using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Engagement.Count;

public sealed record CountEngagementQuery(DateTimeOffset? From, DateTimeOffset? To, string? TrustedTenantId)
    : IQuery<Result<EngagementCountDto>>;

public sealed record EngagementCountDto(long Opens, long Clicks);
