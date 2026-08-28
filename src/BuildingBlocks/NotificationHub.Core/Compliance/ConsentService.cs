using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Common;

namespace NotificationHub.Core.Compliance;

public sealed class ConsentService : IConsentService
{
    private readonly NotificationDbContext _db;
    public ConsentService(NotificationDbContext db) => _db = db;

    public async Task<ConsentRecord> RecordAsync(ConsentRecord record, CancellationToken ct = default)
    {
        _db.ConsentLedger.Add(new ConsentLedgerEntity
        {
            Id = ServerIds.New(),
            SubjectId = record.SubjectId,
            TenantId = record.TenantId,
            Purpose = record.Purpose.Trim().ToLowerInvariant(),
            Channel = string.IsNullOrWhiteSpace(record.Channel) ? null : record.Channel.Trim().ToLowerInvariant(),
            Granted = record.Granted,
            Source = record.Source,
            Actor = record.Actor,
            Evidence = record.Evidence,
            OccurredAt = record.OccurredAt == default ? DateTimeOffset.UtcNow : record.OccurredAt
        });
        await _db.SaveChangesAsync(ct);
        return record;
    }

    public async Task<ConsentDecision> EvaluateAsync(string subjectId, string purpose, string? channel = null, string? tenantId = null, CancellationToken ct = default)
    {
        purpose = purpose.Trim().ToLowerInvariant();
        channel = string.IsNullOrWhiteSpace(channel) ? null : channel.Trim().ToLowerInvariant();

        // Latest channel-specific entry wins; else purpose-level (channel null)
        var q = _db.ConsentLedger.AsNoTracking().Where(x => x.SubjectId == subjectId && x.Purpose == purpose);
        q = tenantId is null ? q.Where(x => x.TenantId == null) : q.Where(x => x.TenantId == tenantId);

        var latestChannel = channel is null ? null : await q.Where(x => x.Channel == channel)
            .OrderByDescending(x => x.OccurredAt).FirstOrDefaultAsync(ct);

        var latest = latestChannel ?? await q.Where(x => x.Channel == null)
            .OrderByDescending(x => x.OccurredAt).FirstOrDefaultAsync(ct);

        if (latest is null)
        {
            // Default: transactional/otp allowed without explicit consent; marketing requires grant
            var defaultAllowed = purpose is "transactional" or "otp" or "security";
            return new ConsentDecision
            {
                Allowed = defaultAllowed,
                Reason = defaultAllowed ? null : $"No consent recorded for purpose '{purpose}'"
            };
        }

        return new ConsentDecision
        {
            Allowed = latest.Granted,
            Reason = latest.Granted ? null : $"Consent revoked for purpose '{purpose}'",
            Latest = ToModel(latest)
        };
    }

    public async Task<IReadOnlyList<ConsentRecord>> ListAsync(string subjectId, string? tenantId = null, CancellationToken ct = default)
    {
        var q = _db.ConsentLedger.AsNoTracking().Where(x => x.SubjectId == subjectId);
        q = tenantId is null ? q.Where(x => x.TenantId == null) : q.Where(x => x.TenantId == tenantId);
        var rows = await q.OrderByDescending(x => x.OccurredAt).Take(200).ToListAsync(ct);
        return rows.Select(ToModel).ToList();
    }

    private static ConsentRecord ToModel(ConsentLedgerEntity e) => new()
    {
        Id = e.Id, SubjectId = e.SubjectId, TenantId = e.TenantId, Purpose = e.Purpose,
        Channel = e.Channel, Granted = e.Granted, Source = e.Source, Actor = e.Actor,
        Evidence = e.Evidence, OccurredAt = e.OccurredAt
    };
}
