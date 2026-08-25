using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Preferences;

public sealed class PreferenceService : IPreferenceService
{
    private readonly NotificationDbContext _db;
    public PreferenceService(NotificationDbContext db) => _db = db;

    public async Task<UserPreference?> GetAsync(string userId, string? tenantId = null, CancellationToken ct = default)
    {
        var q = _db.UserPreferences.AsNoTracking().Where(x => x.UserId == userId);
        q = tenantId is null ? q.Where(x => x.TenantId == null) : q.Where(x => x.TenantId == tenantId);
        var e = await q.FirstOrDefaultAsync(ct);
        if (e is null) return null;
        return new UserPreference
        {
            UserId = e.UserId, TenantId = e.TenantId,
            ChannelOptIn = JsonSerializer.Deserialize<Dictionary<string, bool>>(e.ChannelOptInJson) ?? new(),
            CategoryOptIn = JsonSerializer.Deserialize<Dictionary<string, bool>>(e.CategoryOptInJson) ?? new(),
            PreferredChannel = e.PreferredChannel, QuietHoursStart = e.QuietHoursStart,
            QuietHoursEnd = e.QuietHoursEnd, TimeZoneId = e.TimeZoneId, MaxPerDay = e.MaxPerDay,
            UpdatedAt = e.UpdatedAt
        };
    }

    public async Task SaveAsync(UserPreference preference, CancellationToken ct = default)
    {
        var q = _db.UserPreferences.Where(x => x.UserId == preference.UserId);
        q = preference.TenantId is null ? q.Where(x => x.TenantId == null) : q.Where(x => x.TenantId == preference.TenantId);
        var e = await q.FirstOrDefaultAsync(ct);
        if (e is null)
        {
            e = new UserPreferenceEntity { UserId = preference.UserId, TenantId = preference.TenantId };
            _db.UserPreferences.Add(e);
        }
        e.ChannelOptInJson = JsonSerializer.Serialize(preference.ChannelOptIn);
        e.CategoryOptInJson = JsonSerializer.Serialize(preference.CategoryOptIn);
        e.PreferredChannel = preference.PreferredChannel;
        e.QuietHoursStart = preference.QuietHoursStart;
        e.QuietHoursEnd = preference.QuietHoursEnd;
        e.TimeZoneId = preference.TimeZoneId;
        e.MaxPerDay = preference.MaxPerDay;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<(bool Allowed, string? Reason)> CanSendAsync(string userId, string channel, string? category, string? tenantId, CancellationToken ct = default)
    {
        var pref = await GetAsync(userId, tenantId, ct);
        if (pref is null) return (true, null);

        if (pref.ChannelOptIn.TryGetValue(channel, out var channelOk) && !channelOk)
            return (false, $"User opted out of channel '{channel}'");

        if (!string.IsNullOrEmpty(category) && pref.CategoryOptIn.TryGetValue(category, out var catOk) && !catOk)
            return (false, $"User opted out of category '{category}'");

        if (!string.IsNullOrEmpty(pref.QuietHoursStart) && !string.IsNullOrEmpty(pref.QuietHoursEnd))
        {
            try
            {
                var tz = TimeZoneInfo.FindSystemTimeZoneById(pref.TimeZoneId ?? "UTC");
                var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz).TimeOfDay;
                var start = TimeSpan.Parse(pref.QuietHoursStart);
                var end = TimeSpan.Parse(pref.QuietHoursEnd);
                var inQuiet = start <= end ? local >= start && local <= end : local >= start || local <= end;
                if (inQuiet) return (false, "Quiet hours");
            }
            catch { /* ignore invalid tz */ }
        }

        if (pref.MaxPerDay is > 0)
        {
            var since = DateTimeOffset.UtcNow.AddDays(-1);
            var count = await _db.NotificationStatuses.CountAsync(x =>
                x.Recipient == userId && x.CreatedAt >= since &&
                x.Status != DeliveryStatus.Suppressed && x.Status != DeliveryStatus.Cancelled, ct);
            if (count >= pref.MaxPerDay) return (false, "Daily frequency cap reached");
        }

        return (true, null);
    }
}
