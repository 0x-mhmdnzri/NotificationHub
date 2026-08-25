using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Digest;

public sealed class DigestService : IDigestService
{
    private readonly NotificationDbContext _db;
    private readonly ILogger<DigestService> _logger;

    public DigestService(NotificationDbContext db, ILogger<DigestService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<DigestPolicy> SavePolicyAsync(DigestPolicy policy, CancellationToken ct = default)
    {
        var e = await _db.DigestPolicies.FirstOrDefaultAsync(x => x.Key == policy.Key && x.TenantId == policy.TenantId, ct);
        if (e is null)
        {
            e = new DigestPolicyEntity { Id = policy.Id == Guid.Empty ? Guid.NewGuid() : policy.Id };
            _db.DigestPolicies.Add(e);
        }
        e.Key = policy.Key;
        e.TenantId = policy.TenantId;
        e.WindowMinutes = Math.Clamp(policy.WindowMinutes, 1, 24 * 60);
        e.Channel = policy.Channel;
        e.TemplateKey = policy.TemplateKey;
        e.IsActive = policy.IsActive;
        await _db.SaveChangesAsync(ct);
        return ToPolicy(e);
    }

    public async Task BufferAsync(string policyKey, string recipient, string? tenantId, object payload, CancellationToken ct = default)
    {
        _db.DigestBuffers.Add(new DigestBufferEntity
        {
            PolicyKey = policyKey,
            Recipient = recipient,
            TenantId = tenantId,
            PayloadJson = JsonSerializer.Serialize(payload),
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> FlushDueAsync(CancellationToken ct = default)
    {
        var policies = await _db.DigestPolicies.AsNoTracking().Where(x => x.IsActive).ToListAsync(ct);
        var flushed = 0;
        foreach (var p in policies)
        {
            var cutoff = DateTimeOffset.UtcNow.AddMinutes(-p.WindowMinutes);
            var pending = await _db.DigestBuffers
                .Where(x => x.PolicyKey == p.Key && x.FlushedAt == null && x.CreatedAt <= cutoff
                    && (p.TenantId == null || x.TenantId == p.TenantId))
                .ToListAsync(ct);

            foreach (var group in pending.GroupBy(x => new { x.Recipient, x.TenantId }))
            {
                foreach (var row in group)
                    row.FlushedAt = DateTimeOffset.UtcNow;
                flushed += group.Count();
                _logger.LogInformation("Digest flush policy={Policy} recipient={Recipient} count={Count}",
                    p.Key, group.Key.Recipient, group.Count());
                // Actual send is orchestrated by caller/worker via notification API (keeps core thin)
            }
        }
        if (flushed > 0)
            await _db.SaveChangesAsync(ct);
        return flushed;
    }

    private static DigestPolicy ToPolicy(DigestPolicyEntity e) => new()
    {
        Id = e.Id, Key = e.Key, TenantId = e.TenantId, WindowMinutes = e.WindowMinutes,
        Channel = e.Channel, TemplateKey = e.TemplateKey, IsActive = e.IsActive
    };
}
