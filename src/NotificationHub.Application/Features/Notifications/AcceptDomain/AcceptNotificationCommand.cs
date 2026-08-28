using MediatR;
using NotificationHub.Domain.Delivery;
using NotificationHub.Domain.Delivery.ValueObjects;

namespace NotificationHub.Application.Features.Notifications.AcceptDomain;

/// <summary>
/// Application command that creates a Notification aggregate (rich domain model path).
/// Coordinates repository; business rules live on the Aggregate.
/// </summary>
public sealed record AcceptNotificationCommand(
    Guid? Id,
    string Recipient,
    string Channel,
    string TemplateKey,
    NotificationPriority Priority,
    string? IdempotencyKey,
    string? CollapseKey,
    string? TenantId,
    string? Locale,
    string? Category,
    string? CorrelationId,
    string? PreferredProvider,
    bool AllowFallback,
    DateTimeOffset? ScheduledAtUtc,
    IReadOnlyDictionary<string, object?>? Data) : IRequest<AcceptNotificationResult>;

public sealed record AcceptNotificationResult(Guid NotificationId, DeliveryStatus Status);

public sealed class AcceptNotificationHandler(
    INotificationRepository repository) : IRequestHandler<AcceptNotificationCommand, AcceptNotificationResult>
{
    public async Task<AcceptNotificationResult> Handle(AcceptNotificationCommand cmd, CancellationToken ct)
    {
        var id = cmd.Id is { } g && g != Guid.Empty
            ? NotificationId.From(g)
            : NotificationId.New();

        var notification = Notification.Accept(
            id,
            RecipientAddress.Create(cmd.Recipient),
            ChannelCode.Create(cmd.Channel),
            TemplateKey.Create(cmd.TemplateKey),
            cmd.Priority,
            IdempotencyKey.From(cmd.IdempotencyKey),
            CollapseKey.From(cmd.CollapseKey),
            Domain.Common.TenantId.From(cmd.TenantId),
            cmd.Locale,
            cmd.Category,
            cmd.CorrelationId,
            cmd.PreferredProvider,
            cmd.AllowFallback,
            cmd.ScheduledAtUtc,
            cmd.Data,
            DateTimeOffset.UtcNow);

        await repository.AddAsync(notification, ct);
        // Unit of Work / outbox dispatch of DomainEvents is infrastructure responsibility
        return new AcceptNotificationResult(notification.Id.Value, notification.Status);
    }
}
