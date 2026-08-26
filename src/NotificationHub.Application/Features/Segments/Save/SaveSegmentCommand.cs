using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Segments.Save;

public sealed record SaveSegmentCommand(SegmentDefinition Segment, string? TrustedTenantId)
    : ICommand<Result<SegmentDefinition>>;
