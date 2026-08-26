using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Campaigns.Get;

[AuthorizeRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader)]
public sealed record GetCampaignQuery(Guid CampaignId, string? TrustedTenantId)
    : IQuery<Result<BroadcastCampaign>>;
