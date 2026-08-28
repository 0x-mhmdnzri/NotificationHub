using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Campaigns.Start;

[AuthorizeRoles(AppRoles.Admin, AppRoles.Sender)]
public sealed record StartCampaignCommand(Guid CampaignId, string? TrustedTenantId) : ICommand<Result>;
