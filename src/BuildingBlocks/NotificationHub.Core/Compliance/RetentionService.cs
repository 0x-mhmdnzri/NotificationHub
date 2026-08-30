using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Compliance;

public sealed class RetentionService : IRetentionService
{
    private readonly NotificationDbContext _db;
    private readonly RetentionOptions _options;
    private readonly ILogger<RetentionService> _logger;

    public RetentionService(NotificationDbContext db, IOptions<RetentionOptions> options, ILogger<RetentionService> logger)
    {
        _db = db;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<RetentionSweepResult> SweepAsync(CancellationToken ct = default)
    {
        if (!_options.Enabled)
            return new RetentionSweepResult();

        var now = DateTimeOffset.UtcNow;
        var notifCutoff = now.AddDays(-_options.NotificationDays);
        var auditCutoff = now.AddDays(-_options.AuditDays);
        var timelineCutoff = now.AddDays(-_options.TimelineDays);

        var oldNotifs = await _db.NotificationStatuses.Where(x => x.CreatedAt < notifCutoff).ToListAsync(ct);
        _db.NotificationStatuses.RemoveRange(oldNotifs);

        var oldAudits = await _db.AuditEntries.Where(x => x.CreatedAt < auditCutoff).ToListAsync(ct);
        _db.AuditEntries.RemoveRange(oldAudits);

        var oldTimeline = await _db.WorkflowTimelineEvents.Where(x => x.OccurredAt < timelineCutoff).ToListAsync(ct);
        _db.WorkflowTimelineEvents.RemoveRange(oldTimeline);

        // Consent ledger kept longer; only purge very old if configured
        if (_options.ConsentDays > 0)
        {
            var consentCutoff = now.AddDays(-_options.ConsentDays);
            var oldConsents = await _db.ConsentLedger.Where(x => x.OccurredAt < consentCutoff).ToListAsync(ct);
            _db.ConsentLedger.RemoveRange(oldConsents);
        }


        var outboxDeleted = 0;
        var inboxDeleted = 0;
        if (_options.OutboxPublishedDays > 0)
        {
            var outboxCutoff = now.AddDays(-_options.OutboxPublishedDays);
            var oldOutbox = await _db.OutboxMessages
                .Where(x => (x.Status == "published" || x.Status == "failed") && x.CreatedAt < outboxCutoff)
                .ToListAsync(ct);
            _db.OutboxMessages.RemoveRange(oldOutbox);
            outboxDeleted = oldOutbox.Count;
        }
        if (_options.InboxDays > 0)
        {
            var inboxCutoff = now.AddDays(-_options.InboxDays);
            var oldInbox = await _db.InboxMessages.Where(x => x.ProcessedAt < inboxCutoff).ToListAsync(ct);
            _db.InboxMessages.RemoveRange(oldInbox);
            inboxDeleted = oldInbox.Count;
        }

        await _db.SaveChangesAsync(ct);

        var result = new RetentionSweepResult
        {
            NotificationsDeleted = oldNotifs.Count,
            AuditsDeleted = oldAudits.Count,
            TimelineDeleted = oldTimeline.Count,
            OutboxDeleted = outboxDeleted,
            InboxDeleted = inboxDeleted,
            RanAt = now
        };

        _logger.LogInformation(
            "Retention sweep: notifications={N}, audits={A}, timeline={T}, outbox={O}, inbox={I}",
            result.NotificationsDeleted, result.AuditsDeleted, result.TimelineDeleted,
            result.OutboxDeleted, result.InboxDeleted);

        return result;
    }
}

public sealed class RetentionBackgroundWorker : Microsoft.Extensions.Hosting.BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<RetentionOptions> _options;
    private readonly ILogger<RetentionBackgroundWorker> _logger;

    public RetentionBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<RetentionOptions> options,
        ILogger<RetentionBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Retention worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Value.Enabled)
                {
                    using var scope = _scopeFactory.CreateScope();
                    var retention = scope.ServiceProvider.GetRequiredService<IRetentionService>();
                    await retention.SweepAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Retention sweep failed");
            }

            var delay = TimeSpan.FromMinutes(Math.Max(5, _options.Value.SweepIntervalMinutes));
            await Task.Delay(delay, stoppingToken);
        }
    }
}
