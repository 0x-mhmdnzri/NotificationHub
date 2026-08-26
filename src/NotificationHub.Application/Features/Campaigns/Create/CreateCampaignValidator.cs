using FluentValidation;

namespace NotificationHub.Application.Features.Campaigns.Create;

public sealed class CreateCampaignValidator : AbstractValidator<CreateCampaignCommand>
{
    public CreateCampaignValidator()
    {
        RuleFor(x => x.Request.Name).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Request.TemplateKey).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Request.Channels).NotEmpty();
    }
}
