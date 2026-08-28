using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Messaging;

public sealed class EfOutbox : IOutbox
{
    private readonly NotificationDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public EfOutbox(NotificationDbContext db) => _db = db;

    /// <summary>
    /// Stages an outbox row on the shared DbContext. Does not call SaveChanges so the
    /// caller can commit status + outbox in a single transaction (avoids dual-write).
    /// </summary>
    public Task<Guid> AddAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var id = Guid.NewGuid();
        _db.OutboxMessages.Add(new OutboxMessageEntity
        {
            Id = id,
            NotificationId = request.Id,
            PayloadJson = JsonSerializer.Serialize(request, JsonOptions),
            Status = "pending",
            NextAttemptAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        return Task.FromResult(id);
    }
}

public sealed class EfInbox : IInbox
{
    private readonly NotificationDbContext _db;
    public EfInbox(NotificationDbContext db) => _db = db;

    public Task<bool> ExistsAsync(string messageId, CancellationToken ct = default)
        => _db.InboxMessages.AnyAsync(x => x.MessageId == messageId, ct);

    public async Task<bool> TryMarkProcessedAsync(string messageId, CancellationToken ct = default)
    {
        if (await _db.InboxMessages.FindAsync([messageId], ct) is not null)
            return false;
        _db.InboxMessages.Add(new InboxMessageEntity { MessageId = messageId, ProcessedAt = DateTimeOffset.UtcNow });
        try
        {
            await _db.SaveChangesAsync(ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
