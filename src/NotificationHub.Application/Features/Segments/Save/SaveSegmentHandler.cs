using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Segmentation;

namespace NotificationHub.Application.Features.Segments.Save;

public sealed class SaveSegmentHandler(ISegmentService segments)
    : IRequestHandler<SaveSegmentCommand, Result<SegmentDefinition>>
{
    public async Task<Result<SegmentDefinition>> Handle(SaveSegmentCommand request, CancellationToken cancellationToken)
    {
        var seg = request.Segment with { TenantId = request.TrustedTenantId ?? request.Segment.TenantId };
        var saved = await segments.SaveAsync(seg, cancellationToken);
        return Result.Success(saved);
    }
}
