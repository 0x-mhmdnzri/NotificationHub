using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Core.Persistence;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery;
using NotificationHub.Domain.Delivery.ValueObjects;
using DomainDelivery = NotificationHub.Domain.Delivery.DeliveryStatus;
using DomainPriority = NotificationHub.Domain.Delivery.NotificationPriority;
using AbstractionsModels = NotificationHub.Abstractions.Models;

namespace NotificationHub.Infrastructure.Persistence;

public sealed class EfNotificationRepository(NotificationDbContext db) : INotificationRepository
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task<Notification?> GetAsync(NotificationId id, CancellationToken ct = default)
    {
        var e = await db.NotificationStatuses.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id.Value, ct);
        return e is null ? null : MapToDomain(e);
    }

    public Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        db.NotificationStatuses.Add(MapToEntity(notification));
        // Commit via IUnitOfWork so status + outbox share one transaction.
        return Task.CompletedTask;
    }

    public async Task UpdateAsync(Notification notification, CancellationToken ct = default)
    {
        var e = await db.NotificationStatuses.FirstOrDefaultAsync(x => x.Id == notification.Id.Value, ct)
                ?? throw new InvalidOperationException($"Notification {notification.Id} not found.");
        Apply(notification, e);
        // Caller decides SaveChanges via IUnitOfWork for multi-aggregate transactions.
        await Task.CompletedTask;
    }

    internal static Notification MapToDomain(NotificationStatusEntity e)
    {
        Dictionary<string, object?>? data = null;
        if (!string.IsNullOrEmpty(e.PayloadJson))
        {
            try { data = JsonSerializer.Deserialize<Dictionary<string, object?>>(e.PayloadJson, JsonOpts); }
            catch { /* ignore */ }
        }

        return Notification.Rehydrate(
            NotificationId.From(e.Id),
            RecipientAddress.Create(e.Recipient),
            ChannelCode.Create(string.IsNullOrEmpty(e.Channel) ? "email" : e.Channel),
            TemplateKey.Create("unknown"), // template key not stored on status entity historically
            (DomainDelivery)(int)e.Status,
            DomainPriority.Normal,
            IdempotencyKey.From(e.IdempotencyKey),
            CollapseKey.From(e.CollapseKey),
            TenantId.From(e.TenantId),
            null,
            e.Category,
            e.CorrelationId,
            e.ProviderId,
            true,
            e.ScheduledAt,
            e.CreatedAt,
            e.UpdatedAt,
            e.AttemptCount,
            e.ErrorCode,
            e.ErrorMessage,
            e.ProviderId,
            e.ProviderMessageId,
            data);
    }

    internal static NotificationStatusEntity MapToEntity(Notification n) => new()
    {
        Id = n.Id.Value,
        Channel = n.Channel.Value,
        Recipient = n.Recipient.Value,
        Status = (AbstractionsModels.DeliveryStatus)(int)n.Status,
        ProviderId = n.ProviderId,
        ProviderMessageId = n.ProviderMessageId,
        ErrorCode = n.LastErrorCode,
        ErrorMessage = n.LastErrorMessage,
        AttemptCount = n.AttemptCount,
        CreatedAt = n.CreatedAtUtc,
        UpdatedAt = n.ProcessedAtUtc ?? n.CreatedAtUtc,
        ScheduledAt = n.ScheduledAtUtc,
        TenantId = n.TenantId?.Value,
        IdempotencyKey = n.IdempotencyKey?.Value,
        CollapseKey = n.CollapseKey?.Value,
        CorrelationId = n.CorrelationId,
        Category = n.Category,
        PayloadJson = n.Data.Count > 0 ? JsonSerializer.Serialize(n.Data, JsonOpts) : null
    };

    static void Apply(Notification n, NotificationStatusEntity e)
    {
        e.Status = (AbstractionsModels.DeliveryStatus)(int)n.Status;
        e.ProviderId = n.ProviderId;
        e.ProviderMessageId = n.ProviderMessageId;
        e.ErrorCode = n.LastErrorCode;
        e.ErrorMessage = n.LastErrorMessage;
        e.AttemptCount = n.AttemptCount;
        e.UpdatedAt = n.ProcessedAtUtc ?? DateTimeOffset.UtcNow;
        e.ScheduledAt = n.ScheduledAtUtc;
    }
}
