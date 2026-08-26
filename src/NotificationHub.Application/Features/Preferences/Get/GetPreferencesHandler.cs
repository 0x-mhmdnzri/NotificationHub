using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Preferences;

namespace NotificationHub.Application.Features.Preferences.Get;

public sealed class GetPreferencesHandler(IPreferenceService prefs)
    : IRequestHandler<GetPreferencesQuery, Result<UserPreference>>
{
    public async Task<Result<UserPreference>> Handle(GetPreferencesQuery request, CancellationToken cancellationToken)
    {
        var p = await prefs.GetAsync(request.UserId, request.TrustedTenantId, cancellationToken);
        return p is null
            ? Result.Failure<UserPreference>(Error.NotFound("preference.not_found", "Preferences not found."))
            : Result.Success(p);
    }
}
