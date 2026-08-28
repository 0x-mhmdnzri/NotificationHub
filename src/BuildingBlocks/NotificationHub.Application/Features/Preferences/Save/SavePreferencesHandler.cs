using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Preferences;

namespace NotificationHub.Application.Features.Preferences.Save;

public sealed class SavePreferencesHandler(IPreferenceService prefs)
    : IRequestHandler<SavePreferencesCommand, Result>
{
    public async Task<Result> Handle(SavePreferencesCommand request, CancellationToken cancellationToken)
    {
        await prefs.SaveAsync(request.Preference, cancellationToken);
        return Result.Success();
    }
}
