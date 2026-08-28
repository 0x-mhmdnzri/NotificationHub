using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Campaigns;

public interface ICampaignService
{
    Task<BroadcastCampaign> CreateAsync(CreateCampaignRequest request, string? createdBy, CancellationToken ct = default);
    Task<int> AddRecipientsAsync(Guid campaignId, IReadOnlyList<string> addresses, string[]? channels, string? tenantId, CancellationToken ct = default);
    Task<int> ImportCsvAsync(Guid campaignId, Stream csvStream, string? tenantId, CancellationToken ct = default);
    Task StartAsync(Guid campaignId, string? tenantId, CancellationToken ct = default);
    Task CancelAsync(Guid campaignId, string? tenantId, CancellationToken ct = default);
    Task<BroadcastCampaign?> GetAsync(Guid campaignId, string? tenantId, CancellationToken ct = default);
    Task<CampaignProgress> GetProgressAsync(Guid campaignId, string? tenantId, CancellationToken ct = default);
    Task<int> ProcessPendingBatchAsync(int batchSize, CancellationToken ct = default);
}
