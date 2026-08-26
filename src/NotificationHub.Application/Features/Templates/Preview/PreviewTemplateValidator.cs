using FluentValidation;

namespace NotificationHub.Application.Features.Templates.Preview;

public sealed class PreviewTemplateValidator : AbstractValidator<PreviewTemplateQuery>
{
    public PreviewTemplateValidator()
    {
        RuleFor(x => x.Request.Recipient).NotEmpty();
        RuleFor(x => x.Request.TemplateKey).NotEmpty();
    }
}
