using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Segments.Match;

public sealed record MatchSegmentQuery(
    string Key,
    Dictionary<string, object?> Attributes,
    string? TrustedTenantId
) : IQuery<Result<bool>>;
