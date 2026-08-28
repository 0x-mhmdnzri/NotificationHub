using FluentValidation;

namespace NotificationHub.Application.Features.Devices.Register;

public sealed class RegisterDeviceValidator : AbstractValidator<RegisterDeviceCommand>
{
    public RegisterDeviceValidator()
    {
        RuleFor(x => x.Request.UserId).NotEmpty();
        RuleFor(x => x.Request.Token).NotEmpty();
        RuleFor(x => x.Request.Platform).NotEmpty()
            .Must(p => new[] { "apns", "fcm", "webpush", "expo" }.Contains(p, StringComparer.OrdinalIgnoreCase))
            .WithMessage("Platform must be one of: apns, fcm, webpush, expo");
    }
}
