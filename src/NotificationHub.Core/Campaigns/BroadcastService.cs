using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Campaigns;

/// <summary>Compatibility façade: create campaign + recipients + start in one call.</summary>
public sealed class BroadcastService(ICampaignService campaigns, ILogger<BroadcastService> logger) : IBroadcastService
{
    public async Task<BroadcastResult> SendAsync(BroadcastRequest request, CancellationToken ct = default)
    {
        var channels = request.Channels is { Length: > 0 }
            ? request.Channels
            : string.IsNullOrWhiteSpace(request.Channel) ? ["email"] : [request.Channel];

        var campaign = await campaigns.CreateAsync(new CreateCampaignRequest
        {
            Name = request.Name,
            TemplateKey = request.TemplateKey,
            Channels = channels,
            Data = request.Data,
            TenantId = request.TenantId
        }, createdBy: null, ct);

        var recipients = request.Recipients ?? [];
        var added = await campaigns.AddRecipientsAsync(campaign.Id, recipients, null, request.TenantId, ct);
        await campaigns.StartAsync(campaign.Id, request.TenantId, ct);

        logger.LogInformation("Broadcast façade campaign {Id} recipients={N}", campaign.Id, added);
        return new BroadcastResult
        {
            CampaignId = campaign.Id,
            Accepted = added,
            Failed = 0,
            Status = "processing"
        };
    }
}
