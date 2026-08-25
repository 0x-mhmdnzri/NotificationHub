using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Queue;

namespace NotificationHub.Core.Messaging;

public sealed class MessagingHealthService : IMessagingHealthService
{
    private readonly NotificationDbContext _db;
    private readonly IServiceProvider _sp;
    private readonly MessagingHealthOptions _options;
    private readonly RabbitMqOptions _rabbitOptions;
    private readonly ILogger<MessagingHealthService> _logger;

    public MessagingHealthService(
        NotificationDbContext db,
        IServiceProvider sp,
        IOptions<MessagingHealthOptions> options,
        IOptions<RabbitMqOptions> rabbitOptions,
        ILogger<MessagingHealthService> logger)
    {
        _db = db;
        _sp = sp;
        _options = options.Value;
        _rabbitOptions = rabbitOptions.Value;
        _logger = logger;
    }

    public async Task<MessagingHealthSnapshot> CheckAsync(CancellationToken ct = default)
    {
        var pending = await _db.OutboxMessages.AsNoTracking()
            .Where(x => x.Status == "pending")
            .Select(x => x.CreatedAt)
            .ToListAsync(ct);

        var failed = await _db.OutboxMessages.AsNoTracking().CountAsync(x => x.Status == "failed", ct);
        double? oldestAge = pending.Count == 0
            ? null
            : (DateTimeOffset.UtcNow - pending.Min()).TotalSeconds;

        uint? workDepth = null;
        uint? dlqDepth = null;
        var rabbit = _sp.GetService<RabbitMqNotificationQueue>();
        if (rabbit is not null)
        {
            try
            {
                var depths = await rabbit.GetQueueDepthsAsync(ct);
                workDepth = depths.WorkQueue;
                dlqDepth = depths.DeadLetterQueue;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read RabbitMQ queue depths");
            }
        }

        var alerts = new List<string>();
        var outboxLag = pending.Count >= _options.OutboxPendingCountWarning
                        || (oldestAge is not null && oldestAge > _options.OutboxPendingAgeWarningSeconds);
        if (outboxLag)
            alerts.Add($"Outbox lag: pending={pending.Count}, oldestAgeSec={oldestAge:F0}");

        var dlqWarn = dlqDepth is not null && dlqDepth >= _options.DlqDepthWarning;
        if (dlqWarn)
            alerts.Add($"DLQ depth warning: {dlqDepth}");

        if (failed > 0)
            alerts.Add($"Outbox failed messages: {failed}");

        return new MessagingHealthSnapshot
        {
            OutboxPendingCount = pending.Count,
            OutboxFailedCount = failed,
            OldestPendingAgeSeconds = oldestAge,
            WorkQueueDepth = workDepth,
            DlqDepth = dlqDepth,
            ConfiguredPrefetchCount = _rabbitOptions.PrefetchCount,
            OutboxLagWarning = outboxLag,
            DlqWarning = dlqWarn,
            Alerts = alerts,
            CheckedAt = DateTimeOffset.UtcNow
        };
    }
}

public sealed class MessagingHealthMonitorWorker : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<MessagingHealthOptions> _options;
    private readonly ILogger<MessagingHealthMonitorWorker> _logger;

    public MessagingHealthMonitorWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<MessagingHealthOptions> options,
        ILogger<MessagingHealthMonitorWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Messaging health monitor started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var health = scope.ServiceProvider.GetRequiredService<IMessagingHealthService>();
                var snap = await health.CheckAsync(stoppingToken);
                foreach (var alert in snap.Alerts)
                    _logger.LogWarning("MESSAGING_ALERT {Alert}", alert);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Messaging health check failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, _options.Value.PollIntervalSeconds)), stoppingToken);
        }
    }
}
