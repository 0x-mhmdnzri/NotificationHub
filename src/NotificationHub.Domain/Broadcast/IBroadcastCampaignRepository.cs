using NotificationHub.Domain.Broadcast.ValueObjects;

namespace NotificationHub.Domain.Broadcast;

public interface IBroadcastCampaignRepository
{
    Task<BroadcastCampaign?> GetAsync(CampaignId id, CancellationToken ct = default);
    Task AddAsync(BroadcastCampaign campaign, CancellationToken ct = default);
    Task UpdateAsync(BroadcastCampaign campaign, CancellationToken ct = default);
}
