using FluentValidation;

namespace NotificationHub.Application.Features.Notifications.Accept;

public sealed class AcceptNotificationValidator : AbstractValidator<AcceptNotificationCommand>
{
    public AcceptNotificationValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.Request.Recipient).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Request.TemplateKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request)
            .Must(r => !string.IsNullOrWhiteSpace(r.Channel) || r.Channels is { Length: > 0 })
            .WithMessage("Channel or Channels is required.");
    }
}
