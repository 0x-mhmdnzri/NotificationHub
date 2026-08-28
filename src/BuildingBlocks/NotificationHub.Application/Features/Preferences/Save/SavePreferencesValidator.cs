using FluentValidation;

namespace NotificationHub.Application.Features.Preferences.Save;

public sealed class SavePreferencesValidator : AbstractValidator<SavePreferencesCommand>
{
    public SavePreferencesValidator()
    {
        RuleFor(x => x.Preference.UserId).NotEmpty().MaximumLength(256);
    }
}
