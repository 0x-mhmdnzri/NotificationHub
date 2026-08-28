using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Campaigns.Create;

[AuthorizeRoles(AppRoles.Admin, AppRoles.Sender)]
public sealed record CreateCampaignCommand(CreateCampaignRequest Request, string? TrustedTenantId, string? CreatedBy)
    : ICommand<Result<BroadcastCampaign>>, ITransactional;
