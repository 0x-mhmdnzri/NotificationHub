using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Campaigns;

namespace NotificationHub.Application.Features.Campaigns.AddRecipients;

public sealed class AddRecipientsHandler(ICampaignService campaigns)
    : IRequestHandler<AddRecipientsCommand, Result<int>>
{
    public async Task<Result<int>> Handle(AddRecipientsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var n = await campaigns.AddRecipientsAsync(
                request.CampaignId, request.Request.Addresses, request.Request.Channels, request.TrustedTenantId, cancellationToken);
            return Result.Success(n);
        }
        catch (InvalidOperationException ex)
        {
            return Result.Failure<int>(Error.Failure("campaign.invalid", ex.Message));
        }
    }
}
