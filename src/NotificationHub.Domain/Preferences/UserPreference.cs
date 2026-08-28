using NotificationHub.Domain.Common;
using NotificationHub.Domain.Preferences.Events;
using NotificationHub.Domain.Preferences.ValueObjects;

namespace NotificationHub.Domain.Preferences;

/// <summary>
/// Aggregate root for per-user notification preferences (opt-in, quiet hours, daily cap).
/// Consistency boundary: one user+tenant preference document.
/// </summary>
public sealed class UserPreference : AggregateRoot<PreferenceId>
{
    public UserId UserId { get; private set; } = null!;
    public TenantId? TenantId { get; private set; }
    public IReadOnlyDictionary<string, bool> ChannelOptIn { get; private set; } = new Dictionary<string, bool>();
    public IReadOnlyDictionary<string, bool> CategoryOptIn { get; private set; } = new Dictionary<string, bool>();
    public string? PreferredChannel { get; private set; }
    public string? QuietHoursStart { get; private set; }
    public string? QuietHoursEnd { get; private set; }
    public string? TimeZoneId { get; private set; }
    public int? MaxPerDay { get; private set; }
    public string? WeeklyScheduleJson { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private UserPreference() { }

    public static UserPreference Create(
        PreferenceId id,
        UserId userId,
        TenantId? tenantId,
        DateTimeOffset nowUtc)
    {
        var p = new UserPreference
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            UpdatedAtUtc = nowUtc
        };
        p.Raise(new UserPreferenceUpdated(id, userId, tenantId?.Value, nowUtc));
        return p;
    }

    public static UserPreference Rehydrate(
        PreferenceId id,
        UserId userId,
        TenantId? tenantId,
        IReadOnlyDictionary<string, bool> channelOptIn,
        IReadOnlyDictionary<string, bool> categoryOptIn,
        string? preferredChannel,
        string? quietStart,
        string? quietEnd,
        string? timeZoneId,
        int? maxPerDay,
        string? weeklyScheduleJson,
        DateTimeOffset updatedAtUtc)
    {
        return new UserPreference
        {
            Id = id,
            UserId = userId,
            TenantId = tenantId,
            ChannelOptIn = new Dictionary<string, bool>(channelOptIn),
            CategoryOptIn = new Dictionary<string, bool>(categoryOptIn),
            PreferredChannel = preferredChannel,
            QuietHoursStart = quietStart,
            QuietHoursEnd = quietEnd,
            TimeZoneId = timeZoneId,
            MaxPerDay = maxPerDay,
            WeeklyScheduleJson = weeklyScheduleJson,
            UpdatedAtUtc = updatedAtUtc
        };
    }

    public void SetChannelOptIn(string channel, bool optIn, DateTimeOffset nowUtc)
    {
        if (string.IsNullOrWhiteSpace(channel))
            throw new DomainException("Channel cannot be empty.");
        var map = new Dictionary<string, bool>(ChannelOptIn)
        {
            [channel.Trim().ToLowerInvariant()] = optIn
        };
        ChannelOptIn = map;
        Touch(nowUtc);
    }

    public void SetQuietHours(string? start, string? end, string? timeZoneId, DateTimeOffset nowUtc)
    {
        QuietHoursStart = start;
        QuietHoursEnd = end;
        TimeZoneId = timeZoneId;
        Touch(nowUtc);
    }

    public void SetMaxPerDay(int? max, DateTimeOffset nowUtc)
    {
        if (max is < 0)
            throw new DomainException("MaxPerDay cannot be negative.");
        MaxPerDay = max;
        Touch(nowUtc);
    }

    public void SetPreferredChannel(string? channel, DateTimeOffset nowUtc)
    {
        PreferredChannel = string.IsNullOrWhiteSpace(channel) ? null : channel.Trim().ToLowerInvariant();
        Touch(nowUtc);
    }

    public void ReplaceCategoryOptIn(IReadOnlyDictionary<string, bool> categories, DateTimeOffset nowUtc)
    {
        CategoryOptIn = new Dictionary<string, bool>(categories);
        Touch(nowUtc);
    }

    /// <summary>Critical priority bypasses quiet hours but not hard channel opt-out.</summary>
    public bool AllowsChannel(string channel, bool isCritical)
    {
        var key = channel.Trim().ToLowerInvariant();
        if (ChannelOptIn.TryGetValue(key, out var allowed) && !allowed && !isCritical)
            return false;
        if (ChannelOptIn.TryGetValue(key, out var hard) && !hard && isCritical)
            return false; // hard opt-out still blocks critical
        return true;
    }

    private void Touch(DateTimeOffset nowUtc)
    {
        UpdatedAtUtc = nowUtc;
        Raise(new UserPreferenceUpdated(Id, UserId, TenantId?.Value, nowUtc));
    }
}

public interface IUserPreferenceRepository
{
    Task<UserPreference?> GetAsync(UserId userId, TenantId? tenantId, CancellationToken ct = default);
    Task AddAsync(UserPreference preference, CancellationToken ct = default);
    Task UpdateAsync(UserPreference preference, CancellationToken ct = default);
}
