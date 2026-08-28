using System.Text.Json;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Queue;

namespace NotificationHub.Infrastructure.HangfireJobs;

/// <summary>
/// Durable Hangfire worker: load outbox by id → publish RabbitMQ → mark published.
/// At-least-once: crash after publish before mark → retry may republish; inbox must be idempotent.
/// </summary>
public sealed class OutboxDispatchJob(
    NotificationDbContext db,
    IServiceScopeFactory scopeFactory,
    ILogger<OutboxDispatchJob> logger) : IOutboxDispatchJob
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    [AutomaticRetry(Attempts = 5, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    public async Task DispatchAsync(Guid outboxMessageId, CancellationToken cancellationToken)
    {
        var msg = await db.OutboxMessages.FirstOrDefaultAsync(x => x.Id == outboxMessageId, cancellationToken);
        if (msg is null)
        {
            logger.LogWarning("Outbox {OutboxId} not found — skip", outboxMessageId);
            return;
        }

        if (string.Equals(msg.Status, "published", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(msg.Status, "failed", StringComparison.OrdinalIgnoreCase))
            return;

        try
        {
            using var scope = scopeFactory.CreateScope();
            var rabbit = scope.ServiceProvider.GetService<RabbitMqNotificationQueue>();
            if (rabbit is null)
            {
                logger.LogWarning("RabbitMQ not configured; outbox {OutboxId} stays pending", outboxMessageId);
                return;
            }

            using var doc = JsonDocument.Parse(msg.PayloadJson);
            if (doc.RootElement.TryGetProperty("kind", out var kindEl) &&
                kindEl.GetString() == "integration")
            {
                // Integration event: durable mark as published.
                // Optional: publish to events exchange later; consumers must stay idempotent on MessageId.
                logger.LogInformation(
                    "Integration outbox {OutboxId} eventType={EventType} published (logical)",
                    outboxMessageId,
                    doc.RootElement.TryGetProperty("eventType", out var et) ? et.GetString() : "?");
                msg.Status = "published";
                msg.PublishedAt = DateTimeOffset.UtcNow;
                msg.Attempts++;
                msg.LastError = null;
                await db.SaveChangesAsync(cancellationToken);
                return;
            }

            var request = JsonSerializer.Deserialize<NotificationRequest>(msg.PayloadJson, JsonOpts)
                          ?? throw new InvalidOperationException("null outbox payload");

            await rabbit.PublishAsync(request, redeliveryCount: 0, cancellationToken);

            msg.Status = "published";
            msg.PublishedAt = DateTimeOffset.UtcNow;
            msg.Attempts++;
            msg.LastError = null;
            await db.SaveChangesAsync(cancellationToken);

            logger.LogDebug("Outbox {OutboxId} published notification {NotificationId}", outboxMessageId, msg.NotificationId);
        }
        catch (Exception ex)
        {
            msg.Attempts++;
            msg.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
            msg.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, msg.Attempts)));
            msg.Status = msg.Attempts >= 10 ? "failed" : "pending";
            await db.SaveChangesAsync(cancellationToken);
            logger.LogWarning(ex, "Outbox dispatch failed {OutboxId} attempt={Attempt}", outboxMessageId, msg.Attempts);
            throw;
        }
    }
}
