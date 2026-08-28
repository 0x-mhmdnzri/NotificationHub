using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Core.Persistence;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Preferences;
using NotificationHub.Domain.Preferences.ValueObjects;

namespace NotificationHub.Infrastructure.Persistence;

public sealed class EfUserPreferenceRepository(NotificationDbContext db) : IUserPreferenceRepository
{
    public async Task<UserPreference?> GetAsync(UserId userId, TenantId? tenantId, CancellationToken ct = default)
    {
        var q = db.UserPreferences.AsNoTracking().Where(x => x.UserId == userId.Value);
        q = tenantId is null ? q.Where(x => x.TenantId == null) : q.Where(x => x.TenantId == tenantId.Value.Value);
        var e = await q.FirstOrDefaultAsync(ct);
        return e is null ? null : Map(e);
    }

    public async Task AddAsync(UserPreference preference, CancellationToken ct = default)
    {
        db.UserPreferences.Add(MapEntity(preference));
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserPreference preference, CancellationToken ct = default)
    {
        var e = await db.UserPreferences.FirstOrDefaultAsync(x => x.Id == preference.Id.Value, ct)
                ?? throw new InvalidOperationException($"Preference {preference.Id} not found.");
        e.ChannelOptInJson = JsonSerializer.Serialize(preference.ChannelOptIn);
        e.CategoryOptInJson = JsonSerializer.Serialize(preference.CategoryOptIn);
        e.PreferredChannel = preference.PreferredChannel;
        e.QuietHoursStart = preference.QuietHoursStart;
        e.QuietHoursEnd = preference.QuietHoursEnd;
        e.TimeZoneId = preference.TimeZoneId;
        e.MaxPerDay = preference.MaxPerDay;
        e.WeeklyScheduleJson = preference.WeeklyScheduleJson;
        e.UpdatedAt = preference.UpdatedAtUtc;
        await db.SaveChangesAsync(ct);
    }

    static UserPreference Map(UserPreferenceEntity e)
    {
        var channels = SafeDict(e.ChannelOptInJson);
        var cats = SafeDict(e.CategoryOptInJson);
        return UserPreference.Rehydrate(
            PreferenceId.From(e.Id),
            UserId.Create(e.UserId),
            TenantId.From(e.TenantId),
            channels, cats,
            e.PreferredChannel, e.QuietHoursStart, e.QuietHoursEnd, e.TimeZoneId, e.MaxPerDay,
            e.WeeklyScheduleJson, e.UpdatedAt);
    }

    static UserPreferenceEntity MapEntity(UserPreference p) => new()
    {
        Id = p.Id.Value,
        UserId = p.UserId.Value,
        TenantId = p.TenantId?.Value,
        ChannelOptInJson = JsonSerializer.Serialize(p.ChannelOptIn),
        CategoryOptInJson = JsonSerializer.Serialize(p.CategoryOptIn),
        PreferredChannel = p.PreferredChannel,
        QuietHoursStart = p.QuietHoursStart,
        QuietHoursEnd = p.QuietHoursEnd,
        TimeZoneId = p.TimeZoneId,
        MaxPerDay = p.MaxPerDay,
        WeeklyScheduleJson = p.WeeklyScheduleJson,
        UpdatedAt = p.UpdatedAtUtc
    };

    static Dictionary<string, bool> SafeDict(string json)
    {
        try { return JsonSerializer.Deserialize<Dictionary<string, bool>>(json) ?? new(); }
        catch { return new(); }
    }
}
