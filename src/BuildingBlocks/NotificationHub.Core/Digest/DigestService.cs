using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Common;
using NotificationHub.Core.Messaging;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Queue;

namespace NotificationHub.Core.Digest;

public sealed class DigestService : IDigestService
{
    private readonly NotificationDbContext _db;
    private readonly ILogger<DigestService> _logger;
    private readonly NotificationOrchestrator? _orch;
    private readonly INotificationQueue? _queue;

    public DigestService(
        NotificationDbContext db,
        ILogger<DigestService> logger,
        NotificationOrchestrator? orch = null,
        INotificationQueue? queue = null)
    {
        _db = db;
        _logger = logger;
        _orch = orch;
        _queue = queue;
    }

    public async Task<DigestPolicy> SavePolicyAsync(DigestPolicy policy, CancellationToken ct = default)
    {
        var e = await _db.DigestPolicies.FirstOrDefaultAsync(x => x.Key == policy.Key && x.TenantId == policy.TenantId, ct);
        if (e is null)
        {
            e = new DigestPolicyEntity { Id = ServerIds.New() };
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
                var itemPayloads = new List<object?>();
                foreach (var row in group)
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(row.PayloadJson) ? "{}" : row.PayloadJson);
                        itemPayloads.Add(doc.RootElement.Clone());
                    }
                    catch
                    {
                        itemPayloads.Add(null);
                    }
                    row.FlushedAt = DateTimeOffset.UtcNow;
                }
                flushed += group.Count();

                if (_orch is not null && _queue is not null)
                {
                    var nreq = new NotificationRequest
                    {
                        Recipient = group.Key.Recipient,
                        Channel = p.Channel,
                        TemplateKey = p.TemplateKey,
                        TenantId = group.Key.TenantId,
                        Category = $"digest:{p.Key}",
                        CollapseKey = $"digest:{p.Key}:{group.Key.Recipient}:{DateTimeOffset.UtcNow:yyyyMMddHH}",
                        Data = new Dictionary<string, object?>
                        {
                            ["digest_count"] = group.Count(),
                            ["digest_policy"] = p.Key,
                            ["items"] = itemPayloads
                        }
                    };
                    try
                    {
                        var strategy = _db.Database.CreateExecutionStrategy();
                        await strategy.ExecuteAsync(async () =>
                        {
                            await using var tx = await _db.Database.BeginTransactionAsync(ct);
                            var (ok, status) = await _orch.AcceptAsync(nreq, ct);
                            if (ok && status.Status == DeliveryStatus.Queued)
                            {
                                await _queue.EnqueueAsync(nreq, ct);
                                await _db.SaveChangesAsync(ct);
                            }
                            await tx.CommitAsync(ct);
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Digest enqueue failed for {Recipient}", group.Key.Recipient);
                    }
                }

                _logger.LogInformation("Digest flush policy={Policy} recipient={Recipient} count={Count}",
                    p.Key, group.Key.Recipient, group.Count());
            }
        }
        if (flushed > 0)
            await _db.SaveChangesAsync(ct);
        return flushed;
    }

    private static DigestPolicy ToPolicy(DigestPolicyEntity e) => new()
    {
        Id = e.Id,
        Key = e.Key,
        TenantId = e.TenantId,
        WindowMinutes = e.WindowMinutes,
        Channel = e.Channel,
        TemplateKey = e.TemplateKey,
        IsActive = e.IsActive
    };
}
