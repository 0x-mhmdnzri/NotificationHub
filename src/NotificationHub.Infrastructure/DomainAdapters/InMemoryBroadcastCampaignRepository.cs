using System.Collections.Concurrent;
using NotificationHub.Domain.Broadcast;
using NotificationHub.Domain.Broadcast.ValueObjects;

namespace NotificationHub.Infrastructure.DomainAdapters;

public sealed class InMemoryBroadcastCampaignRepository : IBroadcastCampaignRepository
{
    private readonly ConcurrentDictionary<Guid, BroadcastCampaign> _store = new();

    public Task<BroadcastCampaign?> GetAsync(CampaignId id, CancellationToken ct = default)
    {
        _store.TryGetValue(id.Value, out var c);
        return Task.FromResult(c);
    }

    public Task AddAsync(BroadcastCampaign campaign, CancellationToken ct = default)
    {
        if (!_store.TryAdd(campaign.Id.Value, campaign))
            throw new InvalidOperationException($"Campaign {campaign.Id} already exists.");
        return Task.CompletedTask;
    }

    public Task UpdateAsync(BroadcastCampaign campaign, CancellationToken ct = default)
    {
        _store[campaign.Id.Value] = campaign;
        return Task.CompletedTask;
    }
}
