using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Campaigns.Cancel;

[AuthorizeRoles(AppRoles.Admin)]
public sealed record CancelCampaignCommand(Guid CampaignId, string? TrustedTenantId) : ICommand<Result>;
