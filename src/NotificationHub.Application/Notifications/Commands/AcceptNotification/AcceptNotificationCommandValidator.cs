using FluentValidation;

namespace NotificationHub.Application.Notifications.Commands.AcceptNotification;

public sealed class AcceptNotificationCommandValidator : AbstractValidator<AcceptNotificationCommand>
{
    public AcceptNotificationCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Recipient).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Request.TemplateKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request).Must(r =>
                !string.IsNullOrWhiteSpace(r.Channel) || (r.Channels is { Length: > 0 }))
            .WithMessage("Channel or Channels is required.");
    }
}
