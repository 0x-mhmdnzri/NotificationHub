using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Queue;

namespace NotificationHub.Core.Messaging;

/// <summary>Polls outbox and publishes to RabbitMQ (transactional outbox relay).</summary>
public sealed class OutboxRelayWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxRelayWorker> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public OutboxRelayWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxRelayWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox relay started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox relay tick failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task PublishBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var queue = scope.ServiceProvider.GetService<RabbitMqNotificationQueue>();

        // NpgsqlRetryingExecutionStrategy requires user transactions to run inside CreateExecutionStrategy.
        var strategy = db.Database.CreateExecutionStrategy();
        List<OutboxMessageEntity> claimed = [];

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            claimed = await db.OutboxMessages
                .FromSqlRaw("""
                    SELECT * FROM outbox_messages
                    WHERE "Status" = 'pending'
                      AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= NOW())
                    ORDER BY "CreatedAt"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 50
                    """)
                .ToListAsync(ct);

            if (claimed.Count == 0)
            {
                await tx.CommitAsync(ct);
                return;
            }

            foreach (var msg in claimed)
                msg.Status = "publishing";

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });

        if (claimed.Count == 0)
            return;

        if (queue is null)
        {
            // In-memory mode: drain into in-memory queue
            var inMemory = scope.ServiceProvider.GetRequiredService<INotificationQueue>();
            foreach (var msg in claimed)
            {
                try
                {
                    var request = JsonSerializer.Deserialize<NotificationRequest>(msg.PayloadJson, JsonOptions);
                    if (request is null)
                    {
                        msg.Status = "failed";
                        msg.LastError = "null payload";
                        continue;
                    }
                    await inMemory.EnqueueAsync(request, ct);
                    msg.Status = "published";
                    msg.PublishedAt = DateTimeOffset.UtcNow;
                }
                catch (Exception ex)
                {
                    msg.Attempts++;
                    msg.LastError = ex.Message;
                    msg.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, Math.Pow(2, msg.Attempts)));
                    msg.Status = msg.Attempts >= 10 ? "failed" : "pending";
                    _logger.LogWarning(ex, "Outbox in-memory publish failed for {NotificationId}", msg.NotificationId);
                }
            }
            await db.SaveChangesAsync(ct);
            return;
        }

        foreach (var msg in claimed)
        {
            try
            {
                var request = JsonSerializer.Deserialize<NotificationRequest>(msg.PayloadJson, JsonOptions)
                              ?? throw new InvalidOperationException("null payload");
                await queue.PublishAsync(request, redeliveryCount: 0, ct);
                msg.Status = "published";
                msg.PublishedAt = DateTimeOffset.UtcNow;
                msg.Attempts++;
            }
            catch (Exception ex)
            {
                msg.Attempts++;
                msg.LastError = ex.Message;
                msg.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, Math.Pow(2, msg.Attempts)));
                msg.Status = msg.Attempts >= 10 ? "failed" : "pending";
                _logger.LogWarning(ex, "Outbox publish failed for {NotificationId}", msg.NotificationId);
            }
        }

        await db.SaveChangesAsync(ct);
    }
}
