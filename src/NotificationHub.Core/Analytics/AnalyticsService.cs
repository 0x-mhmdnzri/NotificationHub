using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Analytics;

public interface IAnalyticsService
{
    Task<AnalyticsSummary> GetSummaryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, string? tenantId = null, CancellationToken ct = default);
}

public sealed class CostOptions
{
    public const string SectionName = "Costs";
    public List<ProviderCostConfig> Providers { get; set; } = [];
}

public sealed class AnalyticsService : IAnalyticsService
{
    private readonly NotificationDbContext _db;
    private readonly CostOptions _costs;

    public AnalyticsService(NotificationDbContext db, IOptions<CostOptions> costs)
    {
        _db = db;
        _costs = costs.Value;
    }

    public async Task<AnalyticsSummary> GetSummaryAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, string? tenantId = null, CancellationToken ct = default)
    {
        var q = _db.NotificationStatuses.AsNoTracking().AsQueryable();
        if (from.HasValue) q = q.Where(x => x.CreatedAt >= from);
        if (to.HasValue) q = q.Where(x => x.CreatedAt <= to);
        if (!string.IsNullOrEmpty(tenantId)) q = q.Where(x => x.TenantId == tenantId);

        var items = await q.Select(x => new { x.Status, x.Channel, x.ProviderId, x.Cost }).ToListAsync(ct);
        var total = items.Count;
        long Count(DeliveryStatus s) => items.Count(x => x.Status == s);

        var sent = Count(DeliveryStatus.Sent) + Count(DeliveryStatus.Delivered);
        var failed = Count(DeliveryStatus.Failed) + Count(DeliveryStatus.DeadLetter);
        var costMap = _costs.Providers.ToDictionary(x => x.ProviderId, x => x.CostPerMessage, StringComparer.OrdinalIgnoreCase);

        decimal estimated = 0;
        foreach (var item in items.Where(x => x.Status == DeliveryStatus.Sent || x.Status == DeliveryStatus.Delivered))
        {
            if (item.Cost.HasValue) estimated += item.Cost.Value;
            else if (item.ProviderId is not null && costMap.TryGetValue(item.ProviderId, out var c)) estimated += c;
        }

        return new AnalyticsSummary
        {
            Total = total,
            Queued = Count(DeliveryStatus.Queued),
            Sent = sent,
            Failed = failed,
            DeadLetter = Count(DeliveryStatus.DeadLetter),
            Suppressed = Count(DeliveryStatus.Suppressed),
            Scheduled = Count(DeliveryStatus.Scheduled),
            DeliveryRate = total == 0 ? 0 : Math.Round((double)sent / total, 4),
            FailureRate = total == 0 ? 0 : Math.Round((double)failed / total, 4),
            ByChannel = items.GroupBy(x => x.Channel).ToDictionary(g => g.Key, g => (long)g.Count()),
            ByProvider = items.Where(x => x.ProviderId != null).GroupBy(x => x.ProviderId!).ToDictionary(g => g.Key, g => (long)g.Count()),
            EstimatedCost = estimated
        };
    }
}
