using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Templates;

public sealed class PostgresTemplateStore : ITemplateStore
{
    private readonly NotificationDbContext _db;

    public PostgresTemplateStore(NotificationDbContext db) => _db = db;

    public async Task SaveAsync(TemplateDefinition template, CancellationToken ct = default)
    {
        var entity = await _db.Templates.FirstOrDefaultAsync(x =>
            x.Key == template.Key &&
            x.Channel == template.Channel &&
            x.Locale == template.Locale &&
            x.TenantId == template.TenantId, ct);

        if (entity is null)
        {
            entity = new TemplateEntity
            {
                Id = Guid.NewGuid(),
                Key = template.Key,
                Channel = template.Channel,
                Locale = template.Locale,
                TenantId = template.TenantId
            };
            _db.Templates.Add(entity);
        }

        entity.Subject = template.Subject;
        entity.Body = template.Body;
        entity.HtmlBody = template.HtmlBody;
        entity.Version = template.Version;
        entity.IsActive = template.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        if (entity.CreatedAt == default)
            entity.CreatedAt = template.CreatedAt == default ? DateTimeOffset.UtcNow : template.CreatedAt;

        await _db.SaveChangesAsync(ct);
    }

    public async Task<TemplateDefinition?> FindAsync(string key, string channel, string locale, string? tenantId, CancellationToken ct = default)
    {
        // Tenant-specific first
        if (!string.IsNullOrEmpty(tenantId))
        {
            var tenant = await Query(key, channel, locale, tenantId).FirstOrDefaultAsync(ct);
            if (tenant is not null)
                return ToModel(tenant);
        }

        var global = await Query(key, channel, locale, null).FirstOrDefaultAsync(ct);
        if (global is not null)
            return ToModel(global);

        // Locale fallback to en (global)
        if (!string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase))
        {
            var en = await Query(key, channel, "en", null).FirstOrDefaultAsync(ct);
            if (en is not null)
                return ToModel(en);
        }

        return null;
    }

    public async Task<IReadOnlyList<TemplateDefinition>> ListAsync(string? tenantId = null, string? channel = null, CancellationToken ct = default)
    {
        var q = _db.Templates.AsNoTracking().Where(x => x.IsActive);
        if (tenantId is not null)
            q = q.Where(x => x.TenantId == tenantId || x.TenantId == null);
        if (!string.IsNullOrEmpty(channel))
            q = q.Where(x => x.Channel == channel);
        var list = await q.OrderBy(x => x.Key).ThenBy(x => x.Locale).ToListAsync(ct);
        return list.Select(ToModel).ToList();
    }

    public async Task<bool> DeleteAsync(string key, string channel, string locale, string? tenantId, CancellationToken ct = default)
    {
        var entity = await _db.Templates.FirstOrDefaultAsync(x =>
            x.Key == key && x.Channel == channel && x.Locale == locale && x.TenantId == tenantId, ct);
        if (entity is null)
            return false;
        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<TemplateEntity> Query(string key, string channel, string locale, string? tenantId)
        => _db.Templates.AsNoTracking().Where(x =>
            x.IsActive &&
            x.Key == key &&
            x.Channel == channel &&
            x.Locale == locale &&
            x.TenantId == tenantId);

    private static TemplateDefinition ToModel(TemplateEntity e) => new()
    {
        Key = e.Key,
        Channel = e.Channel,
        Locale = e.Locale,
        Subject = e.Subject,
        Body = e.Body,
        HtmlBody = e.HtmlBody,
        Version = e.Version,
        IsActive = e.IsActive,
        TenantId = e.TenantId,
        CreatedAt = e.CreatedAt
    };
}
