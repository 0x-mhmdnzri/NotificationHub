using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Campaigns;

namespace NotificationHub.Application.Features.Campaigns.Create;

public sealed class CreateCampaignHandler(ICampaignService campaigns)
    : IRequestHandler<CreateCampaignCommand, Result<BroadcastCampaign>>
{
    public async Task<Result<BroadcastCampaign>> Handle(CreateCampaignCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        if (!string.IsNullOrEmpty(request.TrustedTenantId))
            req = req with { TenantId = request.TrustedTenantId };
        var created = await campaigns.CreateAsync(req, request.CreatedBy, cancellationToken);
        return Result.Success(created);
    }
}
