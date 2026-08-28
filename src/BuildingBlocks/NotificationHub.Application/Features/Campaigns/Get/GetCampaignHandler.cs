using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Campaigns;

namespace NotificationHub.Application.Features.Campaigns.Get;

public sealed class GetCampaignHandler(ICampaignService campaigns)
    : IRequestHandler<GetCampaignQuery, Result<BroadcastCampaign>>
{
    public async Task<Result<BroadcastCampaign>> Handle(GetCampaignQuery request, CancellationToken cancellationToken)
    {
        var c = await campaigns.GetAsync(request.CampaignId, request.TrustedTenantId, cancellationToken);
        return c is null
            ? Result.Failure<BroadcastCampaign>(Error.NotFound("campaign.not_found", "Campaign not found."))
            : Result.Success(c);
    }
}
