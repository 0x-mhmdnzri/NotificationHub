using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Inbox;

public sealed class InboxFeedService : IInboxFeedService
{
    private readonly NotificationDbContext _db;
    // simple in-process fanout for SSE (single instance); multi-instance needs Redis pub/sub later
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, Channel<InboxItem>> Streams = new();

    public InboxFeedService(NotificationDbContext db) => _db = db;

    public async Task<InboxFeedResponse> GetFeedAsync(string userId, string? tenantId, bool includeArchived = false, int take = 50, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var q = _db.InAppMessages.AsNoTracking().Where(x => x.UserId == userId);
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(x => x.TenantId == tenantId);
        if (!includeArchived) q = q.Where(x => !x.IsArchived);

        var items = await q.OrderByDescending(x => x.CreatedAt).Take(take).ToListAsync(ct);
        var unread = await q.CountAsync(x => !x.IsRead && !x.IsArchived, ct);
        return new InboxFeedResponse
        {
            Items = items.Select(ToItem).ToList(),
            UnreadCount = unread,
            ServerTime = DateTimeOffset.UtcNow
        };
    }

    public async Task<InboxItem> PushAsync(InboxItem item, CancellationToken ct = default)
    {
        var e = new InAppMessageEntity
        {
            Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id,
            UserId = item.UserId,
            TenantId = item.TenantId,
            Title = item.Title,
            Body = item.Body,
            IsRead = false,
            IsArchived = false,
            NotificationId = item.NotificationId,
            Category = item.Category,
            ActionUrl = item.ActionUrl,
            CreatedAt = item.CreatedAt == default ? DateTimeOffset.UtcNow : item.CreatedAt
        };
        _db.InAppMessages.Add(e);
        await _db.SaveChangesAsync(ct);
        var model = ToItem(e);
        Publish(model);
        return model;
    }

    public async Task<bool> MarkReadAsync(Guid id, string userId, string? tenantId, CancellationToken ct = default)
    {
        var e = await FindOwned(id, userId, tenantId, ct);
        if (e is null) return false;
        e.IsRead = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> MarkAllReadAsync(string userId, string? tenantId, CancellationToken ct = default)
    {
        var q = _db.InAppMessages.Where(x => x.UserId == userId && !x.IsRead);
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(x => x.TenantId == tenantId);
        var list = await q.ToListAsync(ct);
        foreach (var e in list) e.IsRead = true;
        await _db.SaveChangesAsync(ct);
        return list.Count;
    }

    public async Task<bool> ArchiveAsync(Guid id, string userId, string? tenantId, CancellationToken ct = default)
    {
        var e = await FindOwned(id, userId, tenantId, ct);
        if (e is null) return false;
        e.IsArchived = true;
        e.IsRead = true;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async IAsyncEnumerable<InboxItem> StreamAsync(string userId, string? tenantId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var key = StreamKey(userId, tenantId);
        var ch = Streams.GetOrAdd(key, _ => Channel.CreateBounded<InboxItem>(100));
        await foreach (var item in ch.Reader.ReadAllAsync(ct))
            yield return item;
    }

    private void Publish(InboxItem item)
    {
        var key = StreamKey(item.UserId, item.TenantId);
        if (Streams.TryGetValue(key, out var ch))
            ch.Writer.TryWrite(item);
    }

    private async Task<InAppMessageEntity?> FindOwned(Guid id, string userId, string? tenantId, CancellationToken ct)
    {
        var e = await _db.InAppMessages.FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId, ct);
        if (e is null) return null;
        if (!string.IsNullOrEmpty(tenantId) && e.TenantId != tenantId) return null;
        return e;
    }

    private static string StreamKey(string userId, string? tenantId) => $"{tenantId ?? "_"}:{userId}";

    private static InboxItem ToItem(InAppMessageEntity e) => new()
    {
        Id = e.Id,
        UserId = e.UserId,
        TenantId = e.TenantId,
        Title = e.Title,
        Body = e.Body,
        IsRead = e.IsRead,
        IsArchived = e.IsArchived,
        NotificationId = e.NotificationId,
        Category = e.Category,
        ActionUrl = e.ActionUrl,
        CreatedAt = e.CreatedAt
    };
}
