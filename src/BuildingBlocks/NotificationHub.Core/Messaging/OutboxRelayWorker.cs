using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Queue;

namespace NotificationHub.Core.Messaging;

public sealed class OutboxRelayOptions
{
    public const string SectionName = "OutboxRelay";
    /// <summary>Max rows claimed per tick (SKIP LOCKED).</summary>
    public int BatchSize { get; set; } = 100;
    /// <summary>Delay when the last claim was empty (back-off).</summary>
    public int IdlePollIntervalMs { get; set; } = 250;
    /// <summary>Delay when work was found (keep draining under load).</summary>
    public int BusyPollIntervalMs { get; set; } = 0;
    /// <summary>Max concurrent prepare+publish tasks. Channel publish is serialized internally.</summary>
    public int PublishConcurrency { get; set; } = 16;
}

/// <summary>
/// Polls transactional outbox and publishes to RabbitMQ with adaptive polling + parallel prepare.
/// Latency path: Accept → Outbox row → this worker → RabbitMQ → delivery worker.
/// Fixed 2s sleep was the dominant enqueue→queue latency; adaptive busy poll removes it under load.
/// </summary>
public sealed class OutboxRelayWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<OutboxRelayOptions> _options;
    private readonly ILogger<OutboxRelayWorker> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public OutboxRelayWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<OutboxRelayOptions> options,
        ILogger<OutboxRelayWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opt = _options.Value;
        _logger.LogInformation(
            "Outbox relay started batch={Batch} idlePoll={Idle}ms busyPoll={Busy}ms publishConcurrency={Pub}",
            opt.BatchSize, opt.IdlePollIntervalMs, opt.BusyPollIntervalMs, opt.PublishConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            var hadWork = false;
            try
            {
                hadWork = await PublishBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox relay tick failed");
                hadWork = false;
            }

            var delay = hadWork
                ? Math.Max(0, opt.BusyPollIntervalMs)
                : Math.Max(50, opt.IdlePollIntervalMs);
            if (delay > 0)
                await Task.Delay(TimeSpan.FromMilliseconds(delay), stoppingToken);
        }
    }

    /// <returns>true if at least one message was claimed.</returns>
    private async Task<bool> PublishBatchAsync(CancellationToken ct)
    {
        var opt = _options.Value;
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var queue = scope.ServiceProvider.GetService<RabbitMqNotificationQueue>();

        var strategy = db.Database.CreateExecutionStrategy();
        List<OutboxMessageEntity> claimed = [];

        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var limit = Math.Clamp(opt.BatchSize, 1, 2000);
            claimed = await db.OutboxMessages
                .FromSqlRaw("""
                    SELECT * FROM outbox_messages
                    WHERE "Status" = 'pending'
                      AND ("NextAttemptAt" IS NULL OR "NextAttemptAt" <= NOW())
                    ORDER BY "CreatedAt"
                    FOR UPDATE SKIP LOCKED
                    LIMIT {0}
                    """, limit)
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
            return false;

        if (queue is null)
        {
            var inMemory = scope.ServiceProvider.GetRequiredService<INotificationQueue>();
            var integrationPublisher = scope.ServiceProvider.GetService<IIntegrationEventPublisher>();
            foreach (var msg in claimed)
            {
                try
                {
                    if (TryGetIntegrationKind(msg.PayloadJson, out var eventType, out var version, out var messageId, out var tenantId, out var correlationId))
                    {
                        if (integrationPublisher is not null)
                        {
                            if (messageId == Guid.Empty)
                                messageId = msg.Id;
                            await integrationPublisher.PublishAsync(eventType, version, messageId, msg.PayloadJson, tenantId, correlationId, ct);
                        }
                        else
                            _logger.LogDebug("Integration outbox {Id} marked published without broker (no IIntegrationEventPublisher)", msg.Id);
                        msg.Status = "published";
                        msg.PublishedAt = DateTimeOffset.UtcNow;
                        msg.Attempts++;
                        msg.LastError = null;
                        continue;
                    }

                    if (!LooksLikeNotificationRequest(msg.PayloadJson))
                    {
                        msg.Status = "failed";
                        msg.LastError = "unrecognized outbox payload (not notification or integration)";
                        _logger.LogWarning("Outbox {Id} unrecognized payload shape", msg.Id);
                        continue;
                    }

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
            return true;
        }

        // Parallel prepare + publish. RabbitMQ publish is serialized via _publishGate on the queue.
        var concurrency = Math.Max(1, opt.PublishConcurrency);
        var integrationPublisher = scope.ServiceProvider.GetService<IIntegrationEventPublisher>();
        await Parallel.ForEachAsync(
            claimed,
            new ParallelOptions { MaxDegreeOfParallelism = concurrency, CancellationToken = ct },
            async (msg, token) =>
            {
                try
                {
                    if (TryGetIntegrationKind(msg.PayloadJson, out var eventType, out var version, out var messageId, out var tenantId, out var correlationId))
                    {
                        if (integrationPublisher is not null)
                        {
                            if (messageId == Guid.Empty)
                                messageId = msg.Id;
                            await integrationPublisher.PublishAsync(eventType, version, messageId, msg.PayloadJson, tenantId, correlationId, token);
                        }
                        msg.Status = "published";
                        msg.PublishedAt = DateTimeOffset.UtcNow;
                        msg.Attempts++;
                        msg.LastError = null;
                        return;
                    }

                    if (!LooksLikeNotificationRequest(msg.PayloadJson))
                    {
                        msg.Status = "failed";
                        msg.LastError = "unrecognized outbox payload (not notification or integration)";
                        _logger.LogWarning("Outbox {Id} unrecognized payload shape", msg.Id);
                        return;
                    }

                    var request = JsonSerializer.Deserialize<NotificationRequest>(msg.PayloadJson, JsonOptions)
                                  ?? throw new InvalidOperationException("null payload");
                    await queue.PublishAsync(request, redeliveryCount: 0, token);
                    msg.Status = "published";
                    msg.PublishedAt = DateTimeOffset.UtcNow;
                    msg.Attempts++;
                    msg.LastError = null;
                }
                catch (Exception ex)
                {
                    msg.Attempts++;
                    msg.LastError = ex.Message.Length > 500 ? ex.Message[..500] : ex.Message;
                    msg.NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(Math.Min(60, Math.Pow(2, msg.Attempts)));
                    msg.Status = msg.Attempts >= 10 ? "failed" : "pending";
                    _logger.LogWarning(ex, "Outbox publish failed for {NotificationId}", msg.NotificationId);
                }
            });

        await db.SaveChangesAsync(ct);
        return true;
    }

    static bool TryGetIntegrationKind(string payloadJson, out string eventType, out int version, out Guid messageId, out string? tenantId, out string? correlationId)
    {
        eventType = "unknown";
        version = 1;
        messageId = Guid.Empty;
        tenantId = null;
        correlationId = null;
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            if (!root.TryGetProperty("kind", out var kindEl) || kindEl.GetString() != "integration")
                return false;
            if (root.TryGetProperty("eventType", out var et))
                eventType = et.GetString() ?? "unknown";
            if (root.TryGetProperty("version", out var ver) && ver.TryGetInt32(out var v))
                version = v;
            if (root.TryGetProperty("messageId", out var mid) && mid.TryGetGuid(out var g))
                messageId = g;
            if (root.TryGetProperty("tenantId", out var tid))
                tenantId = tid.GetString();
            if (root.TryGetProperty("correlationId", out var cid))
                correlationId = cid.GetString();
            return true;
        }
        catch
        {
            return false;
        }
    }

    static bool LooksLikeNotificationRequest(string payloadJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(payloadJson);
            var root = doc.RootElement;
            return root.TryGetProperty("recipient", out _) && root.TryGetProperty("templateKey", out _);
        }
        catch
        {
            return false;
        }
    }
}
