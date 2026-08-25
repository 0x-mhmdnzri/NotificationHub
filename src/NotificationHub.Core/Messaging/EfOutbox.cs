using System.Text.Json;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Messaging;

public sealed class EfOutbox : IOutbox
{
    private readonly NotificationDbContext _db;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public EfOutbox(NotificationDbContext db) => _db = db;

    public async Task AddAsync(NotificationRequest request, CancellationToken ct = default)
    {
        _db.OutboxMessages.Add(new OutboxMessageEntity
        {
            NotificationId = request.Id,
            PayloadJson = JsonSerializer.Serialize(request, JsonOptions),
            Status = "pending",
            NextAttemptAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }
}

public sealed class EfInbox : IInbox
{
    private readonly NotificationDbContext _db;
    public EfInbox(NotificationDbContext db) => _db = db;

    /// <summary>Returns false if already processed (duplicate).</summary>
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
            // unique violation => already processed
            return false;
        }
    }
}
