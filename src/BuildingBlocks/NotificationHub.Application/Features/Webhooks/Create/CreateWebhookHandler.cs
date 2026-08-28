using System.Text.Json;
using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Common;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Application.Features.Webhooks.Create;

public sealed class CreateWebhookHandler(NotificationDbContext db)
    : IRequestHandler<CreateWebhookCommand, Result<WebhookSubscription>>
{
    public async Task<Result<WebhookSubscription>> Handle(CreateWebhookCommand request, CancellationToken cancellationToken)
    {
        var id = ServerIds.New();
        var tenantId = request.TrustedTenantId;
        var sub = request.Subscription;
        db.WebhookSubscriptions.Add(new WebhookSubscriptionEntity
        {
            Id = id,
            Url = sub.Url,
            Secret = sub.Secret,
            EventsJson = JsonSerializer.Serialize(sub.Events),
            TenantId = tenantId,
            IsActive = sub.IsActive
        });
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(sub with { Id = id, TenantId = tenantId });
    }
}
