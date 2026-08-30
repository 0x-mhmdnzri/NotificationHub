using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Common;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Persistence;
using NotificationHub.Domain.Broadcast.ValueObjects;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery.ValueObjects;
using DomainBroadcast = NotificationHub.Domain.Broadcast;

namespace NotificationHub.Core.Campaigns;

public sealed class CampaignService(
    NotificationDbContext db,
    IServiceScopeFactory scopeFactory,
    IOptions<CampaignDispatchOptions> dispatchOptions,
    ILogger<CampaignService> logger) : ICampaignService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<BroadcastCampaign> CreateAsync(CreateCampaignRequest request, string? createdBy, CancellationToken ct = default)
    {
        if (request.Channels is not { Length: > 0 })
            throw new InvalidOperationException("At least one channel is required.");

        var entity = new BroadcastCampaignEntity
        {
            Id = ServerIds.New(),
            Name = request.Name.Trim(),
            TenantId = request.TenantId,
            Status = (int)CampaignStatus.Draft,
            TemplateKey = request.TemplateKey,
            ChannelsJson = JsonSerializer.Serialize(request.Channels.Select(c => c.ToLowerInvariant()).Distinct().ToArray()),
            DataJson = request.Data is null ? null : JsonSerializer.Serialize(request.Data),
            ScheduledAtUtc = request.ScheduledAtUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedBy = createdBy
        };
        db.BroadcastCampaigns.Add(entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Campaign {CampaignId} created status={Status} tenant={TenantId}", entity.Id, CampaignStatus.Draft, (entity.TenantId ?? "").Replace("\r", "_").Replace("\n", "_"));
        return ToModel(entity);
    }

    public async Task<int> AddRecipientsAsync(
        Guid campaignId, IReadOnlyList<string> addresses, string[]? channels, string? tenantId, CancellationToken ct = default)
    {
        var campaign = await GetEntityAsync(campaignId, tenantId, ct)
            ?? throw new InvalidOperationException("Campaign not found.");
        if (campaign.Status is not ((int)CampaignStatus.Draft) and not ((int)CampaignStatus.Scheduled))
            throw new InvalidOperationException("Recipients can only be added while campaign is Draft or Scheduled.");

        var campaignChannels = JsonSerializer.Deserialize<string[]>(campaign.ChannelsJson) ?? [];
        var chans = (channels is { Length: > 0 } ? channels : campaignChannels)
            .Select(c => c.ToLowerInvariant()).Distinct().ToArray();
        if (chans.Length == 0)
            throw new InvalidOperationException("No channels configured.");

        var normalized = addresses
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var added = 0;
        const int batch = 500;
        for (var i = 0; i < normalized.Count; i += batch)
        {
            var slice = normalized.Skip(i).Take(batch);
            foreach (var addr in slice)
            {
                foreach (var ch in chans)
                {
                    var hash = ContentHash(campaignId, addr, ch);
                    if (await db.BroadcastRecipients.AnyAsync(x => x.ContentHash == hash, ct))
                        continue;

                    db.BroadcastRecipients.Add(new BroadcastRecipientEntity
                    {
                        Id = ServerIds.New(),
                        CampaignId = campaignId,
                        Address = addr,
                        Channel = ch,
                        Status = (int)BroadcastRecipientStatus.Pending,
                        Attempts = 0,
                        CreatedAtUtc = DateTimeOffset.UtcNow,
                        ContentHash = hash
                    });
                    added++;
                }
            }

            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                foreach (var e in db.ChangeTracker.Entries<BroadcastRecipientEntity>().ToList())
                    e.State = EntityState.Detached;
            }
        }

        return added;
    }

    public async Task<int> ImportCsvAsync(Guid campaignId, Stream csvStream, string? tenantId, CancellationToken ct = default)
    {
        var addresses = await CsvRecipientParser.ParseAddressesAsync(csvStream, ct);
        return await AddRecipientsAsync(campaignId, addresses, null, tenantId, ct);
    }

    public async Task StartAsync(Guid campaignId, string? tenantId, CancellationToken ct = default)
    {
        var entity = await GetEntityAsync(campaignId, tenantId, ct)
            ?? throw new InvalidOperationException("Campaign not found.");

        var domain = BroadcastCampaignMapper.ToDomain(entity);
        var now = DateTimeOffset.UtcNow;

        if (entity.ScheduledAtUtc is { } sched && sched > now)
        {
            if (domain.Status != DomainBroadcast.CampaignStatus.Scheduled)
                domain.Schedule(sched, now);
            BroadcastCampaignMapper.Apply(domain, entity);
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Campaign {CampaignId} scheduled for {ScheduledAt}", campaignId, sched);
            return;
        }

        domain.Start(now);
        BroadcastCampaignMapper.Apply(domain, entity);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Campaign {CampaignId} started processing via domain aggregate", campaignId);
    }

    public async Task CancelAsync(Guid campaignId, string? tenantId, CancellationToken ct = default)
    {
        var entity = await GetEntityAsync(campaignId, tenantId, ct)
            ?? throw new InvalidOperationException("Campaign not found.");

        var domain = BroadcastCampaignMapper.ToDomain(entity);
        if (DomainBroadcast.CampaignLifecycle.IsTerminal(domain.Status))
        {
            logger.LogWarning("Campaign {CampaignId} already terminal ({Status}); cancel ignored", campaignId, domain.Status);
            return;
        }

        var from = domain.Status;
        domain.Cancel(DateTimeOffset.UtcNow);
        BroadcastCampaignMapper.Apply(domain, entity);

        await db.BroadcastRecipients
            .Where(x => x.CampaignId == campaignId &&
                        (x.Status == (int)BroadcastRecipientStatus.Pending ||
                         x.Status == (int)BroadcastRecipientStatus.Processing))
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, (int)BroadcastRecipientStatus.Cancelled)
                .SetProperty(x => x.ProcessedAtUtc, DateTimeOffset.UtcNow), ct);

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Campaign {CampaignId} cancelled from {FromStatus} via domain aggregate", campaignId, from);
    }

    public async Task<BroadcastCampaign?> GetAsync(Guid campaignId, string? tenantId, CancellationToken ct = default)
    {
        var e = await GetEntityAsync(campaignId, tenantId, ct);
        return e is null ? null : ToModel(e);
    }

    public async Task<CampaignProgress> GetProgressAsync(Guid campaignId, string? tenantId, CancellationToken ct = default)
    {
        var campaign = await GetEntityAsync(campaignId, tenantId, ct)
            ?? throw new InvalidOperationException("Campaign not found.");

        var groups = await db.BroadcastRecipients.AsNoTracking()
            .Where(x => x.CampaignId == campaignId)
            .GroupBy(x => x.Status)
            .Select(g => new { Status = g.Key, Count = g.LongCount() })
            .ToListAsync(ct);

        long C(BroadcastRecipientStatus s) => groups.FirstOrDefault(x => x.Status == (int)s)?.Count ?? 0;

        return new CampaignProgress
        {
            CampaignId = campaignId,
            Status = (CampaignStatus)campaign.Status,
            Total = groups.Sum(x => x.Count),
            Pending = C(BroadcastRecipientStatus.Pending),
            Processing = C(BroadcastRecipientStatus.Processing),
            Queued = C(BroadcastRecipientStatus.Queued),
            Sent = C(BroadcastRecipientStatus.Sent),
            Failed = C(BroadcastRecipientStatus.Failed),
            DeadLettered = C(BroadcastRecipientStatus.DeadLettered),
            Cancelled = C(BroadcastRecipientStatus.Cancelled),
            Skipped = C(BroadcastRecipientStatus.Skipped)
        };
    }

    /// <summary>
    /// Claims a batch of pending recipients for Processing campaigns and accepts notifications via orchestrator (outbox).
    /// Idempotent: unique ContentHash + status transitions.
    /// </summary>
    public async Task<int> ProcessPendingBatchAsync(int batchSize, CancellationToken ct = default)
    {
        var due = await db.BroadcastCampaigns
            .Where(x => x.Status == (int)CampaignStatus.Scheduled &&
                        x.ScheduledAtUtc != null &&
                        x.ScheduledAtUtc <= DateTimeOffset.UtcNow)
            .ToListAsync(ct);
        foreach (var c in due)
        {
            var domain = BroadcastCampaignMapper.ToDomain(c);
            domain.Start(DateTimeOffset.UtcNow);
            BroadcastCampaignMapper.Apply(domain, c);
        }
        if (due.Count > 0)
            await db.SaveChangesAsync(ct);

        var processingIds = await db.BroadcastCampaigns.AsNoTracking()
            .Where(x => x.Status == (int)CampaignStatus.Processing
                     || x.Status == (int)CampaignStatus.Preparing
                     || x.Status == (int)CampaignStatus.Dispatching
                     || x.Status == (int)CampaignStatus.Delivering)
            .Select(x => x.Id)
            .Take(50)
            .ToListAsync(ct);

        if (processingIds.Count == 0)
            return 0;

        var batch = await db.BroadcastRecipients
            .Where(x => processingIds.Contains(x.CampaignId) &&
                        x.Status == (int)BroadcastRecipientStatus.Pending)
            .OrderBy(x => x.CreatedAtUtc)
            .Take(batchSize)
            .ToListAsync(ct);

        if (batch.Count == 0)
        {
            foreach (var cid in processingIds)
            {
                var remaining = await db.BroadcastRecipients.CountAsync(
                    x => x.CampaignId == cid &&
                         (x.Status == (int)BroadcastRecipientStatus.Pending ||
                          x.Status == (int)BroadcastRecipientStatus.Processing), ct);
                if (remaining == 0)
                {
                    var camp = await db.BroadcastCampaigns.FirstAsync(x => x.Id == cid, ct);
                    var total = await db.BroadcastRecipients.CountAsync(x => x.CampaignId == cid, ct);
                    var sent = await db.BroadcastRecipients.CountAsync(x => x.CampaignId == cid && x.Status == (int)BroadcastRecipientStatus.Sent, ct);
                    var failed = await db.BroadcastRecipients.CountAsync(x => x.CampaignId == cid && (x.Status == (int)BroadcastRecipientStatus.Failed || x.Status == (int)BroadcastRecipientStatus.DeadLettered), ct);
                    var cancelled = await db.BroadcastRecipients.CountAsync(x => x.CampaignId == cid && x.Status == (int)BroadcastRecipientStatus.Cancelled, ct);
                    var skipped = await db.BroadcastRecipients.CountAsync(x => x.CampaignId == cid && x.Status == (int)BroadcastRecipientStatus.Skipped, ct);
                    var domain = BroadcastCampaignMapper.ToDomain(camp);
                    domain.CompleteWithCounts(total, sent, failed, cancelled, skipped, DateTimeOffset.UtcNow);
                    BroadcastCampaignMapper.Apply(domain, camp);
                    var next = domain.Status;
                    logger.LogInformation(
                        "Campaign {CampaignId} completed status={Status} total={Total} sent={Sent} failed={Failed}",
                        cid, next, total, sent, failed);
                }
            }
            await db.SaveChangesAsync(ct);
            return 0;
        }

        foreach (var row in batch)
            row.Status = (int)BroadcastRecipientStatus.Processing;
        await db.SaveChangesAsync(ct);

        var campaigns = await db.BroadcastCampaigns.AsNoTracking()
            .Where(x => processingIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);

        var concurrency = Math.Max(1, dispatchOptions.Value.AcceptConcurrency);
        var results = new ConcurrentDictionary<Guid, (int Status, Guid? NotificationId, string? ErrorCode, string? ErrorMessage, int Attempts)>();

        await Parallel.ForEachAsync(
            batch,
            new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = ct },
            async (row, token) =>
            {
                if (!campaigns.TryGetValue(row.CampaignId, out var camp))
                    return;

                if (camp.Status == (int)CampaignStatus.Cancelled)
                {
                    results[row.Id] = ((int)BroadcastRecipientStatus.Cancelled, null, null, null, row.Attempts);
                    return;
                }

                Dictionary<string, object?> data = new();
                if (camp.DataJson is not null)
                {
                    var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(camp.DataJson);
                    if (raw is not null)
                        foreach (var kv in raw)
                            data[kv.Key] = kv.Value;
                }

                var nreq = new NotificationRequest
                {
                    Recipient = row.Address,
                    Channel = row.Channel,
                    TemplateKey = camp.TemplateKey,
                    Data = data,
                    TenantId = camp.TenantId,
                    IdempotencyKey = row.ContentHash,
                    CorrelationId = camp.Id.ToString("N")
                };

                try
                {
                    // Own scope per parallel Accept — DbContext is not thread-safe.
                    await using var scope = scopeFactory.CreateAsyncScope();
                    var orch = scope.ServiceProvider.GetRequiredService<NotificationOrchestrator>();
                    var (ok, status) = await orch.AcceptAsync(nreq, token);
                    var attempts = row.Attempts + 1;
                    int st;
                    string? errCode = null, errMsg = null;
                    if (ok && status.Status is DeliveryStatus.Queued or DeliveryStatus.Sent or DeliveryStatus.Delivered)
                        st = (int)BroadcastRecipientStatus.Queued;
                    else if (status.Status == DeliveryStatus.Suppressed)
                    {
                        st = (int)BroadcastRecipientStatus.Skipped;
                        errMsg = status.ErrorMessage;
                    }
                    else
                    {
                        st = attempts >= 5
                            ? (int)BroadcastRecipientStatus.DeadLettered
                            : (int)BroadcastRecipientStatus.Pending;
                        errCode = status.ErrorCode;
                        errMsg = status.ErrorMessage;
                    }
                    results[row.Id] = (st, status.NotificationId, errCode, errMsg, attempts);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Broadcast recipient {RecipientId} campaign={CampaignId} failed", row.Id, row.CampaignId);
                    var attempts = row.Attempts + 1;
                    var st = attempts >= 5
                        ? (int)BroadcastRecipientStatus.DeadLettered
                        : (int)BroadcastRecipientStatus.Pending;
                    var msg = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    results[row.Id] = (st, null, null, msg, attempts);
                }
            });

        var processed = 0;
        foreach (var row in batch)
        {
            if (!results.TryGetValue(row.Id, out var r))
                continue;
            row.Status = r.Status;
            row.NotificationId = r.NotificationId;
            row.ErrorCode = r.ErrorCode;
            row.ErrorMessage = r.ErrorMessage;
            row.Attempts = r.Attempts;
            row.ProcessedAtUtc = DateTimeOffset.UtcNow;
            processed++;
        }

        await db.SaveChangesAsync(ct);
        return processed;
    }

    private async Task<BroadcastCampaignEntity?> GetEntityAsync(Guid id, string? tenantId, CancellationToken ct)
    {
        var q = db.BroadcastCampaigns.Where(x => x.Id == id);
        if (tenantId is not null)
            q = q.Where(x => x.TenantId == tenantId);
        return await q.FirstOrDefaultAsync(ct);
    }

    private static string ContentHash(Guid campaignId, string address, string channel)
    {
        var raw = $"{campaignId:N}|{channel}|{address}".ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..32];
    }

    private static BroadcastCampaign ToModel(BroadcastCampaignEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        TenantId = e.TenantId,
        Status = (CampaignStatus)e.Status,
        TemplateKey = e.TemplateKey,
        Channels = JsonSerializer.Deserialize<string[]>(e.ChannelsJson) ?? [],
        Data = e.DataJson is null ? null : JsonSerializer.Deserialize<Dictionary<string, string>>(e.DataJson),
        ScheduledAtUtc = e.ScheduledAtUtc,
        CreatedAtUtc = e.CreatedAtUtc,
        StartedAtUtc = e.StartedAtUtc,
        CompletedAtUtc = e.CompletedAtUtc,
        CreatedBy = e.CreatedBy
    };
}
