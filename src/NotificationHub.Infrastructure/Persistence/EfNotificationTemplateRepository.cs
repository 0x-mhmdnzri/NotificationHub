using Microsoft.EntityFrameworkCore;
using NotificationHub.Core.Persistence;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery.ValueObjects;
using NotificationHub.Domain.Templates;

namespace NotificationHub.Infrastructure.Persistence;

public sealed class EfNotificationTemplateRepository(NotificationDbContext db) : INotificationTemplateRepository
{
    public async Task<NotificationTemplate?> GetByKeyAsync(TemplateKey key, string? tenantId, string? locale, CancellationToken ct = default)
    {
        var loc = string.IsNullOrWhiteSpace(locale) ? "en" : locale;
        var q = db.Templates.AsNoTracking().Where(x => x.Key == key.Value && x.IsActive);
        q = tenantId is null ? q.Where(x => x.TenantId == null) : q.Where(x => x.TenantId == tenantId);
        var e = await q.Where(x => x.Locale == loc).OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct)
                ?? await q.Where(x => x.Locale == "en").OrderByDescending(x => x.Version).FirstOrDefaultAsync(ct);
        return e is null ? null : Map(e);
    }

    public async Task AddAsync(NotificationTemplate template, CancellationToken ct = default)
    {
        db.Templates.Add(MapEntity(template));
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(NotificationTemplate template, CancellationToken ct = default)
    {
        var e = await db.Templates.FirstOrDefaultAsync(x => x.Id == template.Id.Value, ct)
                ?? throw new InvalidOperationException($"Template {template.Id} not found.");
        e.Subject = template.Subject;
        e.Body = template.Body;
        e.HtmlBody = template.HtmlBody;
        e.Version = template.Version;
        e.UpdatedAt = template.UpdatedAtUtc;
        await db.SaveChangesAsync(ct);
    }

    static NotificationTemplate Map(TemplateEntity e) =>
        NotificationTemplate.Rehydrate(
            TemplateId.From(e.Id),
            TemplateKey.Create(e.Key),
            e.Channel,
            e.Subject,
            e.Body,
            e.HtmlBody,
            e.Locale,
            TenantId.From(e.TenantId),
            e.Version,
            e.UpdatedAt);

    static TemplateEntity MapEntity(NotificationTemplate t) => new()
    {
        Id = t.Id.Value,
        Key = t.Key.Value,
        Channel = t.Channel,
        Locale = t.Locale,
        TenantId = t.TenantId?.Value,
        Subject = t.Subject,
        Body = t.Body,
        HtmlBody = t.HtmlBody,
        Version = t.Version,
        IsActive = true,
        CreatedAt = t.UpdatedAtUtc,
        UpdatedAt = t.UpdatedAtUtc
    };
}
