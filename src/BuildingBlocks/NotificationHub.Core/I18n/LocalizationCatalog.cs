using Microsoft.EntityFrameworkCore;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.I18n;

public sealed class LocalizationCatalog : ILocalizationCatalog
{
    private readonly NotificationDbContext _db;
    public LocalizationCatalog(NotificationDbContext db) => _db = db;

    public async Task SetAsync(string key, string locale, string value, string? tenantId = null, CancellationToken ct = default)
    {
        var e = await _db.LocalizationEntries.FirstOrDefaultAsync(x =>
            x.Key == key && x.Locale == locale && x.TenantId == tenantId, ct);
        if (e is null)
        {
            e = new LocalizationEntryEntity { Key = key, Locale = locale, TenantId = tenantId };
            _db.LocalizationEntries.Add(e);
        }
        e.Value = value;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<string?> GetAsync(string key, string locale, string? tenantId = null, CancellationToken ct = default)
    {
        var e = await _db.LocalizationEntries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == key && x.Locale == locale && x.TenantId == tenantId, ct);
        if (e is not null)
            return e.Value;
        // fallback en
        if (!string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase))
        {
            e = await _db.LocalizationEntries.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == key && x.Locale == "en" && x.TenantId == tenantId, ct);
            return e?.Value;
        }
        return null;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllAsync(string locale, string? tenantId = null, CancellationToken ct = default)
    {
        var q = _db.LocalizationEntries.AsNoTracking().Where(x => x.Locale == locale);
        q = tenantId is null ? q.Where(x => x.TenantId == null) : q.Where(x => x.TenantId == tenantId);
        var list = await q.ToListAsync(ct);
        return list.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
    }
}
