using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Campaigns.AddRecipients;

[AuthorizeRoles(AppRoles.Admin, AppRoles.Sender)]
public sealed record AddRecipientsCommand(Guid CampaignId, AddRecipientsRequest Request, string? TrustedTenantId)
    : ICommand<Result<int>>;
