using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Common;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Topics;

public sealed class TopicService : ITopicService
{
    private readonly NotificationDbContext _db;
    public TopicService(NotificationDbContext db) => _db = db;

    public async Task<TopicDefinition> SaveTopicAsync(TopicDefinition topic, CancellationToken ct = default)
    {
        var e = await _db.Topics.FirstOrDefaultAsync(x => x.Key == topic.Key && x.TenantId == topic.TenantId, ct);
        if (e is null)
        {
            e = new TopicEntity { Id = ServerIds.New() };
            _db.Topics.Add(e);
        }
        e.Key = topic.Key;
        e.Name = topic.Name ?? topic.Key;
        e.TenantId = topic.TenantId;
        e.IsActive = topic.IsActive;
        await _db.SaveChangesAsync(ct);
        return new TopicDefinition { Id = e.Id, Key = e.Key, Name = e.Name, TenantId = e.TenantId, IsActive = e.IsActive };
    }

    public async Task SubscribeAsync(string topicKey, string subscriberId, string? tenantId, string? channel, string? address, CancellationToken ct = default)
    {
        var exists = await _db.TopicSubscribers.AnyAsync(x =>
            x.TopicKey == topicKey && x.SubscriberId == subscriberId && x.TenantId == tenantId, ct);
        if (exists)
            return;
        _db.TopicSubscribers.Add(new TopicSubscriberEntity
        {
            TopicKey = topicKey,
            SubscriberId = subscriberId,
            TenantId = tenantId,
            Channel = channel,
            Address = address
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task UnsubscribeAsync(string topicKey, string subscriberId, string? tenantId, CancellationToken ct = default)
    {
        var rows = await _db.TopicSubscribers
            .Where(x => x.TopicKey == topicKey && x.SubscriberId == subscriberId && x.TenantId == tenantId)
            .ToListAsync(ct);
        _db.TopicSubscribers.RemoveRange(rows);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<TopicSubscriber>> ListSubscribersAsync(string topicKey, string? tenantId, CancellationToken ct = default)
    {
        var q = _db.TopicSubscribers.AsNoTracking().Where(x => x.TopicKey == topicKey);
        if (!string.IsNullOrEmpty(tenantId))
            q = q.Where(x => x.TenantId == tenantId);
        var list = await q.ToListAsync(ct);
        return list.Select(x => new TopicSubscriber
        {
            Id = x.Id,
            TopicKey = x.TopicKey,
            SubscriberId = x.SubscriberId,
            TenantId = x.TenantId,
            Channel = x.Channel,
            Address = x.Address
        }).ToList();
    }

    public async Task<IReadOnlyList<TopicDefinition>> ListTopicsAsync(string? tenantId, CancellationToken ct = default)
    {
        var q = _db.Topics.AsNoTracking().Where(x => x.IsActive);
        if (!string.IsNullOrEmpty(tenantId))
            q = q.Where(x => x.TenantId == tenantId || x.TenantId == null);
        var list = await q.ToListAsync(ct);
        return list.Select(x => new TopicDefinition { Id = x.Id, Key = x.Key, Name = x.Name, TenantId = x.TenantId, IsActive = x.IsActive }).ToList();
    }
}
