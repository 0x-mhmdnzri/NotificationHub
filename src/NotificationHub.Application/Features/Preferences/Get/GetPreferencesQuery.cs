using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Preferences.Get;

public sealed record GetPreferencesQuery(string UserId, string? TrustedTenantId)
    : IQuery<Result<UserPreference>>;
