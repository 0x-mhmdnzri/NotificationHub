using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Queue;

namespace NotificationHub.Core.Campaigns;

public sealed class BroadcastService : IBroadcastService
{
    private readonly NotificationDbContext _db;
    private readonly NotificationOrchestrator _orch;
    private readonly INotificationQueue _queue;
    private readonly ILogger<BroadcastService> _logger;

    public BroadcastService(
        NotificationDbContext db,
        NotificationOrchestrator orch,
        INotificationQueue queue,
        ILogger<BroadcastService> logger)
    {
        _db = db;
        _orch = orch;
        _queue = queue;
        _logger = logger;
    }

    public async Task<BroadcastResult> SendAsync(BroadcastRequest request, CancellationToken ct = default)
    {
        var campaignId = Guid.NewGuid();
        var recipients = new List<string>();
        if (request.Recipients is { Count: > 0 })
            recipients.AddRange(request.Recipients);

        // F33: when SegmentKey set, pull CDP profiles for tenant (simple audience = all known emails)
        if (!string.IsNullOrWhiteSpace(request.SegmentKey))
        {
            var q = _db.CdpProfiles.AsNoTracking().Where(x => x.Email != null);
            if (!string.IsNullOrEmpty(request.TenantId))
                q = q.Where(x => x.TenantId == request.TenantId);
            var emails = await q.Select(x => x.Email!).Take(10_000).ToListAsync(ct);
            recipients.AddRange(emails);
            _logger.LogInformation("Broadcast {Name} segment {Seg} expanded to {N} CDP emails",
                request.Name, request.SegmentKey, emails.Count);
        }

        recipients = recipients.Distinct(StringComparer.OrdinalIgnoreCase).Take(10_000).ToList();
        var accepted = 0;
        var failed = 0;

        foreach (var r in recipients)
        {
            try
            {
                var nreq = new NotificationRequest
                {
                    Recipient = r,
                    Channel = request.Channel,
                    TemplateKey = request.TemplateKey,
                    Data = request.Data,
                    TenantId = request.TenantId,
                    Locale = request.Locale,
                    Category = $"campaign:{request.Name}",
                    CollapseKey = $"campaign:{campaignId}:{r}"
                };
                var strategy = _db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await _db.Database.BeginTransactionAsync(ct);
                    var (ok, status) = await _orch.AcceptAsync(nreq, ct);
                    if (!ok) { await tx.RollbackAsync(ct); Interlocked.Increment(ref failed); return; }
                    if (status.Status == DeliveryStatus.Queued)
                    {
                        await _queue.EnqueueAsync(nreq, ct);
                        await _db.SaveChangesAsync(ct);
                    }
                    await tx.CommitAsync(ct);
                    Interlocked.Increment(ref accepted);
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Broadcast recipient failed {R}", r);
                Interlocked.Increment(ref failed);
            }
        }

        return new BroadcastResult
        {
            CampaignId = campaignId,
            Accepted = accepted,
            Failed = failed,
            Status = "completed"
        };
    }
}
