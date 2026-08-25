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
        if (queue is null)
        {
            // In-memory mode: drain outbox into in-memory queue
            var inMemory = scope.ServiceProvider.GetRequiredService<INotificationQueue>();
            var pendingMem = await db.OutboxMessages
                .Where(x => x.Status == "pending" && (x.NextAttemptAt == null || x.NextAttemptAt <= DateTimeOffset.UtcNow))
                .OrderBy(x => x.CreatedAt).Take(50).ToListAsync(ct);
            foreach (var msg in pendingMem)
            {
                var request = JsonSerializer.Deserialize<NotificationRequest>(msg.PayloadJson, JsonOptions);
                if (request is null) { msg.Status = "failed"; msg.LastError = "null payload"; continue; }
                await inMemory.EnqueueAsync(request, ct);
                msg.Status = "published";
                msg.PublishedAt = DateTimeOffset.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            return;
        }

        var pending = await db.OutboxMessages
            .Where(x => x.Status == "pending" && (x.NextAttemptAt == null || x.NextAttemptAt <= DateTimeOffset.UtcNow))
            .OrderBy(x => x.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        foreach (var msg in pending)
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
                if (msg.Attempts >= 10)
                    msg.Status = "failed";
                _logger.LogWarning(ex, "Outbox publish failed for {NotificationId}", msg.NotificationId);
            }
        }

        if (pending.Count > 0)
            await db.SaveChangesAsync(ct);
    }
}
