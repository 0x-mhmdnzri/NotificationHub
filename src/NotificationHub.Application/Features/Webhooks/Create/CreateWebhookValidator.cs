using FluentValidation;
using NotificationHub.Core.Webhooks;

namespace NotificationHub.Application.Features.Webhooks.Create;

public sealed class CreateWebhookValidator : AbstractValidator<CreateWebhookCommand>
{
    public CreateWebhookValidator()
    {
        RuleFor(x => x.Subscription.Url).NotEmpty()
            .Must(url => WebhookUrlValidator.IsSafe(url, out _))
            .WithMessage("Webhook URL is not allowed (SSRF protection).");
    }
}
