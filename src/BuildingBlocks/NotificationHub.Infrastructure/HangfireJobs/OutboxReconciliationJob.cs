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
    /// <summary>Require Admin role on /hangfire (in addition to valid API key).</summary>
    public bool DashboardRequireAdmin { get; set; } = true;
    /// <summary>Dev-only: allow dashboard without API key when true.</summary>
    public bool DashboardAllowAnonymousInDevelopment { get; set; } = false;
    /// <summary>
    /// When true, run a dedicated Hangfire server process that ONLY polls the critical queue
    /// with CriticalWorkerCount workers so bulk jobs cannot starve OTP/security dispatch.
    /// </summary>
    public bool DedicatedCriticalServer { get; set; } = true;
    /// <summary>Workers exclusive to queue "critical" (default: max(4, ProcessorCount)).</summary>
    public int CriticalWorkerCount { get; set; } = 0;
    /// <summary>Workers for notifications + outbox + default (default: max(2, ProcessorCount)).</summary>
    public int StandardWorkerCount { get; set; } = 0;
    /// <summary>Max Hangfire dashboard requests per minute per API key (or IP if no key).</summary>
    public int DashboardRateLimitPerMinute { get; set; } = 30;
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
        scheduler.ScheduleDispatchBatch(stuck, MessagingQueues.Outbox);
    }
}
