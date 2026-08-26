using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Segmentation;

namespace NotificationHub.Application.Features.Segments.Match;

public sealed class MatchSegmentHandler(ISegmentService segments)
    : IRequestHandler<MatchSegmentQuery, Result<bool>>
{
    public async Task<Result<bool>> Handle(MatchSegmentQuery request, CancellationToken cancellationToken)
    {
        var match = await segments.MatchesAsync(request.Key, request.Attributes, request.TrustedTenantId, cancellationToken);
        return Result.Success(match);
    }
}
