using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Preferences.Save;

public sealed record SavePreferencesCommand(UserPreference Preference) : ICommand<Result>;
