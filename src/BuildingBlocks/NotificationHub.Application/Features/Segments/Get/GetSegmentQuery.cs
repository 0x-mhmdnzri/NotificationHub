using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Segments.Get;

public sealed record GetSegmentQuery(string Key, string? TrustedTenantId)
    : IQuery<Result<SegmentDefinition>>;
