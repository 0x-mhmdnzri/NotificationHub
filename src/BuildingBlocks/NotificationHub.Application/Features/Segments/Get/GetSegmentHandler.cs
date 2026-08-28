using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Segmentation;

namespace NotificationHub.Application.Features.Segments.Get;

public sealed class GetSegmentHandler(ISegmentService segments)
    : IRequestHandler<GetSegmentQuery, Result<SegmentDefinition>>
{
    public async Task<Result<SegmentDefinition>> Handle(GetSegmentQuery request, CancellationToken cancellationToken)
    {
        var seg = await segments.GetAsync(request.Key, request.TrustedTenantId, cancellationToken);
        return seg is null
            ? Result.Failure<SegmentDefinition>(Error.NotFound("segment.not_found", "Segment not found."))
            : Result.Success(seg);
    }
}
