using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Preferences;

public interface IPreferenceService
{
    Task<UserPreference?> GetAsync(string userId, string? tenantId = null, CancellationToken ct = default);
    Task SaveAsync(UserPreference preference, CancellationToken ct = default);
    Task<(bool Allowed, string? Reason)> CanSendAsync(string userId, string channel, string? category, string? tenantId, CancellationToken ct = default);
}
