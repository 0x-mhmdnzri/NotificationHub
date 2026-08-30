using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Common;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Segmentation;

public interface ISegmentService
{
    Task<SegmentDefinition> SaveAsync(SegmentDefinition segment, CancellationToken ct = default);
    Task<SegmentDefinition?> GetAsync(string key, string? tenantId = null, CancellationToken ct = default);
    Task<bool> MatchesAsync(string segmentKey, Dictionary<string, object?> attributes, string? tenantId = null, CancellationToken ct = default);
}

public sealed class SegmentService : ISegmentService
{
    private readonly NotificationDbContext _db;
    public SegmentService(NotificationDbContext db) => _db = db;

    public async Task<SegmentDefinition> SaveAsync(SegmentDefinition segment, CancellationToken ct = default)
    {
        var e = await _db.Segments.FirstOrDefaultAsync(x => x.Key == segment.Key && x.TenantId == segment.TenantId, ct);
        if (e is null)
        {
            e = new SegmentDefinitionEntity { Id = ServerIds.New(), Key = segment.Key, TenantId = segment.TenantId };
            _db.Segments.Add(e);
        }
        e.MatchAll = segment.MatchAll;
        e.RulesJson = JsonSerializer.Serialize(segment.Rules);
        await _db.SaveChangesAsync(ct);
        return segment;
    }

    public async Task<SegmentDefinition?> GetAsync(string key, string? tenantId = null, CancellationToken ct = default)
    {
        var q = _db.Segments.AsNoTracking().Where(x => x.Key == key);
        q = tenantId is null ? q.Where(x => x.TenantId == null) : q.Where(x => x.TenantId == tenantId);
        var e = await q.FirstOrDefaultAsync(ct);
        if (e is null)
            return null;
        return new SegmentDefinition
        {
            Id = e.Id,
            Key = e.Key,
            TenantId = e.TenantId,
            MatchAll = e.MatchAll,
            Rules = JsonSerializer.Deserialize<List<SegmentRule>>(e.RulesJson) ?? []
        };
    }

    public async Task<bool> MatchesAsync(string segmentKey, Dictionary<string, object?> attributes, string? tenantId = null, CancellationToken ct = default)
    {
        var segment = await GetAsync(segmentKey, tenantId, ct);
        if (segment is null || segment.Rules.Count == 0)
            return false;

        bool Match(SegmentRule rule)
        {
            attributes.TryGetValue(rule.Field, out var raw);
            var value = raw?.ToString() ?? "";
            return rule.Operator.ToLowerInvariant() switch
            {
                "eq" => string.Equals(value, rule.Value, StringComparison.OrdinalIgnoreCase),
                "neq" => !string.Equals(value, rule.Value, StringComparison.OrdinalIgnoreCase),
                "contains" => value.Contains(rule.Value, StringComparison.OrdinalIgnoreCase),
                "in" => rule.Value.Split(',').Select(x => x.Trim()).Contains(value, StringComparer.OrdinalIgnoreCase),
                _ => false
            };
        }

        return segment.MatchAll ? segment.Rules.All(Match) : segment.Rules.Any(Match);
    }
}
