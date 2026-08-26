using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Engagement;

namespace NotificationHub.Application.Features.Engagement.Count;

public sealed class CountEngagementHandler(IEngagementService engagement)
    : IRequestHandler<CountEngagementQuery, Result<EngagementCountDto>>
{
    public async Task<Result<EngagementCountDto>> Handle(CountEngagementQuery request, CancellationToken cancellationToken)
    {
        var (opens, clicks) = await engagement.CountAsync(request.From, request.To, request.TrustedTenantId, cancellationToken);
        return Result.Success(new EngagementCountDto(opens, clicks));
    }
}
