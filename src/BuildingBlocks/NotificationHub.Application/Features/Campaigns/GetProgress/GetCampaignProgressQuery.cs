using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Campaigns.GetProgress;

[AuthorizeRoles(AppRoles.Admin, AppRoles.Sender, AppRoles.Reader)]
public sealed record GetCampaignProgressQuery(Guid CampaignId, string? TrustedTenantId)
    : IQuery<Result<CampaignProgress>>;
