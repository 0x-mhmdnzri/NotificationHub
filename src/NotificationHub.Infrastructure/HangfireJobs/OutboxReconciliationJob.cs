using NotificationHub.Core.Messaging;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Infrastructure.HangfireJobs;

public sealed class HangfireMessagingOptions
{
    public const string SectionName = "HangfireMessaging";
    public bool Enabled { get; set; } = true;
    /// <summary>Keep polling OutboxRelayWorker as safety net when true.</summary>
    public bool KeepRelayWorker { get; set; } = true;
    public int ReconciliationIntervalMinutes { get; set; } = 2;
    /// <summary>Only re-dispatch pending rows older than this (seconds).</summary>
    public int StuckPendingSeconds { get; set; } = 30;
    public int ReconciliationBatchSize { get; set; } = 100;
}

/// <summary>
/// Safety net: find stuck pending outbox rows and re-enqueue Hangfire dispatch jobs.
/// Event-driven enqueue is primary; this is reconciliation (skill rule 37).
/// </summary>
public sealed class OutboxReconciliationJob(
    NotificationDbContext db,
    IOutboxDispatchScheduler scheduler,
    IOptions<HangfireMessagingOptions> options,
    ILogger<OutboxReconciliationJob> logger)
{
    [DisableConcurrentExecution(timeoutInSeconds: 60)]
    public async Task ReconcileAsync(CancellationToken cancellationToken)
    {
        var opt = options.Value;
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-Math.Max(5, opt.StuckPendingSeconds));
        var stuck = await db.OutboxMessages.AsNoTracking()
            .Where(x => x.Status == "pending" &&
                        (x.NextAttemptAt == null || x.NextAttemptAt <= DateTimeOffset.UtcNow) &&
                        x.CreatedAt <= cutoff)
            .OrderBy(x => x.CreatedAt)
            .Take(opt.ReconciliationBatchSize)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        if (stuck.Count == 0)
            return;

        logger.LogInformation("Outbox reconciliation enqueueing {Count} stuck messages", stuck.Count);
        scheduler.ScheduleDispatchBatch(stuck);
    }
}
