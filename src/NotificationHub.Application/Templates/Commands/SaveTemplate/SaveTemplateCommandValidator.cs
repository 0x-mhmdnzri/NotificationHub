using FluentValidation;

namespace NotificationHub.Application.Templates.Commands.SaveTemplate;

public sealed class SaveTemplateCommandValidator : AbstractValidator<SaveTemplateCommand>
{
    public SaveTemplateCommandValidator()
    {
        RuleFor(x => x.Template.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Template.Channel).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Template.Body).NotEmpty();
        RuleFor(x => x.Template.Locale).NotEmpty().MaximumLength(16);
    }
}
