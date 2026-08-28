using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Campaigns;

namespace NotificationHub.Application.Features.Campaigns.GetProgress;

public sealed class GetCampaignProgressHandler(ICampaignService campaigns)
    : IRequestHandler<GetCampaignProgressQuery, Result<CampaignProgress>>
{
    public async Task<Result<CampaignProgress>> Handle(GetCampaignProgressQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var p = await campaigns.GetProgressAsync(request.CampaignId, request.TrustedTenantId, cancellationToken);
            return Result.Success(p);
        }
        catch (InvalidOperationException)
        {
            return Result.Failure<CampaignProgress>(Error.NotFound("campaign.not_found", "Campaign not found."));
        }
    }
}
