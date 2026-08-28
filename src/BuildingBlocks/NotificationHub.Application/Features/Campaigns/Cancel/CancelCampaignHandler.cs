using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Campaigns;

namespace NotificationHub.Application.Features.Campaigns.Cancel;

public sealed class CancelCampaignHandler(ICampaignService campaigns)
    : IRequestHandler<CancelCampaignCommand, Result>
{
    public async Task<Result> Handle(CancelCampaignCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await campaigns.CancelAsync(request.CampaignId, request.TrustedTenantId, cancellationToken);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Failure("campaign.cancel_failed", ex.Message));
        }
    }
}
