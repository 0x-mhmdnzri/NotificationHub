using FluentValidation;

namespace NotificationHub.Application.Features.Templates.Save;

public sealed class SaveTemplateValidator : AbstractValidator<SaveTemplateCommand>
{
    public SaveTemplateValidator()
    {
        RuleFor(x => x.Template.Key).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Template.Channel).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Template.Body).NotEmpty();
        RuleFor(x => x.Template.Locale).NotEmpty().MaximumLength(16);
    }
}
