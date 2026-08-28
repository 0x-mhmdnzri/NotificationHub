using Microsoft.EntityFrameworkCore;
using NotificationHub.Core.Campaigns;
using NotificationHub.Core.Persistence;
using NotificationHub.Domain.Broadcast;
using NotificationHub.Domain.Broadcast.ValueObjects;

namespace NotificationHub.Infrastructure.Persistence;

public sealed class EfBroadcastCampaignRepository(NotificationDbContext db) : IBroadcastCampaignRepository
{
    public async Task<BroadcastCampaign?> GetAsync(CampaignId id, CancellationToken ct = default)
    {
        var e = await db.BroadcastCampaigns.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id.Value, ct);
        return e is null ? null : BroadcastCampaignMapper.ToDomain(e);
    }

    public async Task AddAsync(BroadcastCampaign campaign, CancellationToken ct = default)
    {
        db.BroadcastCampaigns.Add(BroadcastCampaignMapper.ToEntity(campaign));
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(BroadcastCampaign campaign, CancellationToken ct = default)
    {
        var e = await db.BroadcastCampaigns.FirstOrDefaultAsync(x => x.Id == campaign.Id.Value, ct)
                ?? throw new InvalidOperationException($"Campaign {campaign.Id} not found.");
        BroadcastCampaignMapper.Apply(campaign, e);
        await db.SaveChangesAsync(ct);
    }
}
