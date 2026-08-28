using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Preferences;

public interface IPreferenceService
{
    Task<UserPreference?> GetAsync(string userId, string? tenantId = null, CancellationToken ct = default);
    Task SaveAsync(UserPreference preference, CancellationToken ct = default);
    /// <param name="isCritical">F11 — critical bypasses quiet hours and weekly schedule (not hard opt-out).</param>
    Task<(bool Allowed, string? Reason)> CanSendAsync(string userId, string channel, string? category, string? tenantId, bool isCritical = false, CancellationToken ct = default);

    /// <summary>F10 — stable embed contract for preference centers.</summary>
    Task<PreferenceEmbedModel> GetEmbedModelAsync(string userId, string? tenantId = null, CancellationToken ct = default);
}

public sealed record PreferenceEmbedModel
{
    public required string UserId { get; init; }
    public string? TenantId { get; init; }
    public IReadOnlyList<PreferenceChannelOption> Channels { get; init; } = [];
    public IReadOnlyList<PreferenceCategoryOption> Categories { get; init; } = [];
    public string? PreferredChannel { get; init; }
    public string? QuietHoursStart { get; init; }
    public string? QuietHoursEnd { get; init; }
    public string? TimeZoneId { get; init; }
    public int? MaxPerDay { get; init; }
    public Dictionary<string, string>? WeeklySchedule { get; init; }
    public string SchemaVersion { get; init; } = "1.0";
}

public sealed record PreferenceChannelOption(string Channel, bool Enabled);
public sealed record PreferenceCategoryOption(string Category, bool Enabled);
