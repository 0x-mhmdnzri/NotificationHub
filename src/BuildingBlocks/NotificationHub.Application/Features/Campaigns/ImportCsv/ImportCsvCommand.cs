using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Campaigns.ImportCsv;

[AuthorizeRoles(AppRoles.Admin, AppRoles.Sender)]
public sealed record ImportCsvCommand(Guid CampaignId, Stream CsvStream, string? TrustedTenantId)
    : ICommand<Result<int>>;
