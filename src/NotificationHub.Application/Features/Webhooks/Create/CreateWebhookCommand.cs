using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Webhooks.Create;

public sealed record CreateWebhookCommand(WebhookSubscription Subscription, string? TrustedTenantId)
    : ICommand<Result<WebhookSubscription>>;
