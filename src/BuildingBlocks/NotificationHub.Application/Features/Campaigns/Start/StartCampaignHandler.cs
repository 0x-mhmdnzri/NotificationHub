using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Campaigns;

namespace NotificationHub.Application.Features.Campaigns.Start;

public sealed class StartCampaignHandler(ICampaignService campaigns)
    : IRequestHandler<StartCampaignCommand, Result>
{
    public async Task<Result> Handle(StartCampaignCommand request, CancellationToken cancellationToken)
    {
        try
        {
            await campaigns.StartAsync(request.CampaignId, request.TrustedTenantId, cancellationToken);
            return Result.Success();
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure(Error.Failure("campaign.start_failed", ex.Message));
        }
    }
}
