using Microsoft.Extensions.Caching.Memory;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Preferences;

public sealed class CachingPreferenceService : IPreferenceService
{
    private readonly PreferenceService _inner;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);

    public CachingPreferenceService(PreferenceService inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    private static string CacheKey(string userId, string? tenantId) => $"pref:{tenantId ?? "_"}:{userId}";

    public async Task<UserPreference?> GetAsync(string userId, string? tenantId = null, CancellationToken ct = default)
    {
        var k = CacheKey(userId, tenantId);
        if (_cache.TryGetValue(k, out UserPreference? cached))
            return cached;
        var pref = await _inner.GetAsync(userId, tenantId, ct);
        _cache.Set(k, pref, new MemoryCacheEntryOptions { AbsoluteExpirationRelativeToNow = Ttl });
        return pref;
    }

    public async Task SaveAsync(UserPreference preference, CancellationToken ct = default)
    {
        await _inner.SaveAsync(preference, ct);
        _cache.Remove(CacheKey(preference.UserId, preference.TenantId));
    }

    public async Task<(bool Allowed, string? Reason)> CanSendAsync(
        string userId, string channel, string? category, string? tenantId, bool isCritical = false, CancellationToken ct = default)
    {
        var pref = await GetAsync(userId, tenantId, ct);
        if (pref is null) return (true, null);
        if (pref.ChannelOptIn.TryGetValue(channel, out var channelOk) && !channelOk)
            return (false, $"User opted out of channel '{channel}'");
        if (!string.IsNullOrEmpty(category) && pref.CategoryOptIn.TryGetValue(category, out var catOk) && !catOk)
            return (false, $"User opted out of category '{category}'");
        if (isCritical) return (true, null);
        if (pref.MaxPerDay is int max && max > 0)
            return await _inner.CanSendAsync(userId, channel, category, tenantId, isCritical, ct);
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
            catch { }
        }
        return (true, null);
    }

    public Task<PreferenceEmbedModel> GetEmbedModelAsync(string userId, string? tenantId = null, CancellationToken ct = default)
        => _inner.GetEmbedModelAsync(userId, tenantId, ct);
}
