using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Audit;
using NotificationHub.Core.Compliance;
using NotificationHub.Core.Messaging;
// Hangfire schedule after COMMIT via IOutboxDispatchScheduler
using NotificationHub.Core.Common;
using NotificationHub.Core.PluginHost;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Routing;
using NotificationHub.Core.Store;
using NotificationHub.Core.Templates;
using NotificationHub.Core.Webhooks;
using NotificationHub.Core.Observability;
using NotificationHub.Domain.Events;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Orchestration;

public sealed class NotificationOrchestrator
{
    private readonly PluginLoader _pluginLoader;
    private readonly ITemplateEngine _templateEngine;
    private readonly INotificationStatusStore _statusStore;
    private readonly IPreferenceService _preferences;
    private readonly IConsentService _consents;
    private readonly IOutbox _outbox;
    private readonly IOutboxDispatchScheduler _outboxScheduler;
    private readonly IDomainEventDispatcher? _domainEvents;
    private readonly IAuditService _audit;
    private readonly IWebhookDispatcher _webhooks;
    private readonly IProviderRouter _router;
    private readonly IProviderHealthTracker _health;
    private readonly ILogger<NotificationOrchestrator> _logger;
    private readonly IMetricsService? _metrics;
    private readonly NotificationDbContext _db;
    private const int MaxRetries = 3;

    public NotificationOrchestrator(
        PluginLoader pluginLoader,
        ITemplateEngine templateEngine,
        INotificationStatusStore statusStore,
        IPreferenceService preferences,
        IConsentService consents,
        IOutbox outbox,
        IOutboxDispatchScheduler outboxScheduler,
        IAuditService audit,
        IWebhookDispatcher webhooks,
        IProviderRouter router,
        IProviderHealthTracker health,
        ILogger<NotificationOrchestrator> logger,
        NotificationDbContext db,
        IMetricsService? metrics = null,
        IDomainEventDispatcher? domainEvents = null)
    {
        _pluginLoader = pluginLoader;
        _templateEngine = templateEngine;
        _statusStore = statusStore;
        _preferences = preferences;
        _consents = consents;
        _outbox = outbox;
        _outboxScheduler = outboxScheduler;
        _audit = audit;
        _webhooks = webhooks;
        _router = router;
        _health = health;
        _logger = logger;
        _metrics = metrics;
        _db = db;
        _domainEvents = domainEvents;
    }

    public async Task<(bool Accepted, NotificationStatus Status)> AcceptAsync(NotificationRequest request, CancellationToken ct = default)
    {
        request = request with { Id = ServerIds.New() };
        var channel = ResolveChannel(request);

        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existing = await _statusStore.GetByIdempotencyKeyAsync(request.IdempotencyKey, request.TenantId, ct).ConfigureAwait(false);
            if (existing is not null)
            {
                _metrics?.Increment("notifications.idempotent_hit", 1, ("channel", channel));
                return (true, existing);
            }
        }

        if (!string.IsNullOrWhiteSpace(request.CollapseKey))
        {
            var existingCollapse = await _statusStore.FindByCollapseKeyAsync(request.CollapseKey!, request.Recipient, request.TenantId, ct).ConfigureAwait(false);
            if (existingCollapse is not null)
            {
                _metrics?.Increment("notifications.collapse_hit", 1, ("channel", channel));
                return (true, existingCollapse);
            }
        }

        _metrics?.Increment("notifications.accept", 1, ("channel", channel));

        // Sequential on shared DbContext (EF is not concurrent). Cache hides preference RTT.
        var isCritical = request.Priority == NotificationPriority.Critical;
        var purpose = string.IsNullOrWhiteSpace(request.Category) ? "transactional" : request.Category!;
        var (allowed, reason) = await _preferences.CanSendAsync(request.Recipient, channel, request.Category, request.TenantId, isCritical, ct).ConfigureAwait(false);
        if (!allowed)
        {
            var (suppressed, _) = await PersistSuppressedDomainAsync(request, channel, reason ?? "preference_denied", ct).ConfigureAwait(false);
            await FireAudit("suppressed", request.Id, request.TenantId, reason);
            _metrics?.Increment("notifications.suppressed", 1, ("reason", "preference"));
            return (true, suppressed);
        }

        var consent = await _consents.EvaluateAsync(request.Recipient, purpose, channel, request.TenantId, ct).ConfigureAwait(false);
        if (!consent.Allowed)
        {
            var (suppressed, _) = await PersistSuppressedDomainAsync(request, channel, consent.Reason ?? "consent_denied", ct).ConfigureAwait(false);
            await FireAudit("suppressed", request.Id, request.TenantId, consent.Reason);
            _metrics?.Increment("notifications.suppressed", 1, ("reason", "consent"));
            return (true, suppressed);
        }

        var now = DateTimeOffset.UtcNow;
        // Rich domain model owns delivery lifecycle invariants (Queued vs Scheduled).
        var domain = NotificationHub.Domain.Delivery.Notification.Accept(
            NotificationHub.Domain.Delivery.ValueObjects.NotificationId.From(request.Id),
            NotificationHub.Domain.Delivery.ValueObjects.RecipientAddress.Create(request.Recipient),
            NotificationHub.Domain.Delivery.ValueObjects.ChannelCode.Create(channel),
            NotificationHub.Domain.Delivery.ValueObjects.TemplateKey.Create(request.TemplateKey),
            (NotificationHub.Domain.Delivery.NotificationPriority)(int)request.Priority,
            NotificationHub.Domain.Delivery.ValueObjects.IdempotencyKey.From(request.IdempotencyKey),
            NotificationHub.Domain.Delivery.ValueObjects.CollapseKey.From(request.CollapseKey),
            NotificationHub.Domain.Common.TenantId.From(request.TenantId),
            request.Locale,
            request.Category,
            request.CorrelationId,
            request.PreferredProvider,
            request.AllowFallback,
            request.ScheduledAt,
            request.Data,
            now);

        var status = MakeStatus(request, channel, (DeliveryStatus)(int)domain.Status, null);
        status = status with { CreatedAt = domain.CreatedAtUtc, UpdatedAt = domain.CreatedAtUtc };

        var strategy = _db.Database.CreateExecutionStrategy();
        Guid? outboxId = null;
        await strategy.ExecuteAsync(
            state: 0,
            operation: async (dbCtx, _, token) =>
            {
                await using var tx = await dbCtx.Database.BeginTransactionAsync(token).ConfigureAwait(false);
                await _statusStore.SaveAsync(status, token).ConfigureAwait(false);
                if (domain.Status == NotificationHub.Domain.Delivery.DeliveryStatus.Queued)
                    outboxId = await _outbox.AddAsync(request, token).ConfigureAwait(false);
                await dbCtx.SaveChangesAsync(token).ConfigureAwait(false);
                await tx.CommitAsync(token).ConfigureAwait(false);
                return true;
            },
            verifySucceeded: null,
            cancellationToken: ct).ConfigureAwait(false);

        // After COMMIT only — Hangfire durable dispatch by outbox id (not full payload).
        if (outboxId is { } oid)
            _outboxScheduler.ScheduleDispatch(oid);

        // Domain → Integration event outbox (separate payloads, kind=integration).
        if (_domainEvents is not null)
        {
            await _domainEvents.DispatchAsync(domain.DomainEvents, ct).ConfigureAwait(false);
            domain.ClearDomainEvents();
            await _db.SaveChangesAsync(ct).ConfigureAwait(false);
            // Schedule any new integration outbox rows
            // (Hangfire reconciliation will pick pending if we don't track ids here)
        }

        await FireAudit("accepted", request.Id, request.TenantId, channel);
        return (true, status);
    }


    /// <summary>
    /// Preference/consent deny: Aggregate Accept → Suppress, persist status + integration events (no delivery outbox).
    /// </summary>
    private async Task<(NotificationStatus Status, NotificationHub.Domain.Delivery.Notification Domain)> PersistSuppressedDomainAsync(
        NotificationRequest request, string channel, string reason, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        var domain = NotificationHub.Domain.Delivery.Notification.Accept(
            NotificationHub.Domain.Delivery.ValueObjects.NotificationId.From(request.Id),
            NotificationHub.Domain.Delivery.ValueObjects.RecipientAddress.Create(request.Recipient),
            NotificationHub.Domain.Delivery.ValueObjects.ChannelCode.Create(channel),
            NotificationHub.Domain.Delivery.ValueObjects.TemplateKey.Create(
                string.IsNullOrWhiteSpace(request.TemplateKey) ? "suppressed" : request.TemplateKey),
            (NotificationHub.Domain.Delivery.NotificationPriority)(int)request.Priority,
            NotificationHub.Domain.Delivery.ValueObjects.IdempotencyKey.From(request.IdempotencyKey),
            NotificationHub.Domain.Delivery.ValueObjects.CollapseKey.From(request.CollapseKey),
            NotificationHub.Domain.Common.TenantId.From(request.TenantId),
            request.Locale,
            request.Category,
            request.CorrelationId,
            request.PreferredProvider,
            request.AllowFallback,
            request.ScheduledAt,
            request.Data,
            now);

        domain.Suppress(reason, now);

        var status = MakeStatus(request, channel, DeliveryStatus.Suppressed, reason);
        status = status with { CreatedAt = domain.CreatedAtUtc, UpdatedAt = now };

        var strategy = _db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(
            state: 0,
            operation: async (dbCtx, _, token) =>
            {
                await using var tx = await dbCtx.Database.BeginTransactionAsync(token).ConfigureAwait(false);
                await _statusStore.SaveAsync(status, token).ConfigureAwait(false);
                if (_domainEvents is not null)
                    await _domainEvents.DispatchAsync(domain.DomainEvents, token).ConfigureAwait(false);
                await dbCtx.SaveChangesAsync(token).ConfigureAwait(false);
                await tx.CommitAsync(token).ConfigureAwait(false);
                return true;
            },
            verifySucceeded: null,
            cancellationToken: ct).ConfigureAwait(false);

        domain.ClearDomainEvents();
        return (status, domain);
    }

    private static NotificationStatus MakeStatus(NotificationRequest request, string channel, DeliveryStatus st, string? error) => new()
    {
        NotificationId = request.Id,
        Channel = channel,
        Recipient = request.Recipient,
        Status = st,
        ScheduledAt = request.ScheduledAt,
        TenantId = request.TenantId,
        IdempotencyKey = request.IdempotencyKey,
        CollapseKey = request.CollapseKey,
        CorrelationId = request.CorrelationId,
        Category = request.Category,
        ErrorMessage = error,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private Task FireAudit(string action, Guid notificationId, string? tenantId, string? details)
        => _audit.LogAsync(action, notificationId, tenantId, details: details);

    public async Task<DeliveryResult> ProcessAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var channel = ResolveChannel(request);
        _metrics?.Increment("notifications.process", 1, ("channel", channel));
        var plugins = _router.Resolve(channel, request.PreferredProvider, request.AllowFallback).ToList();

        if (plugins.Count == 0)
        {
            var fail = new DeliveryResult { Success = false, ErrorCode = "NO_PLUGIN", ErrorMessage = $"No plugin for channel '{channel}'" };
            await FinalizeAsync(request, fail, ct);
            return fail;
        }

        var rendered = await _templateEngine.RenderAsync(request with { Channel = channel }, ct);
        rendered = rendered with { PreferredProvider = request.PreferredProvider, Attachments = request.Attachments };

        DeliveryResult? lastResult = null;

        foreach (var plugin in plugins)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                try
                {
                    _logger.LogInformation("Send {Id} via {Plugin} attempt {Attempt}", request.Id, plugin.Id, attempt);
                    await _statusStore.UpdateProviderAsync(request.Id, plugin.Id, ct);

                    var result = await plugin.SendAsync(rendered, ct);
                    result = result with { AttemptNumber = attempt, ProviderId = plugin.Id };

                    if (result.Success)
                    {
                        _health.RecordSuccess(plugin.Id, channel);
                        await FinalizeAsync(request, result, ct);
                        return result;
                    }

                    _health.RecordFailure(plugin.Id, channel, result.ErrorCode);
                    lastResult = result;
                    _logger.LogWarning("Provider {Plugin} failed: {Error}", plugin.Id, result.ErrorMessage);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Provider {Plugin} exception", plugin.Id);
                    _health.RecordFailure(plugin.Id, channel, "EXCEPTION");
                    lastResult = new DeliveryResult { Success = false, ProviderId = plugin.Id, ErrorCode = "EXCEPTION", ErrorMessage = ex.Message, AttemptNumber = attempt };
                }

                if (attempt < MaxRetries)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }

            if (!request.AllowFallback) break;
            _logger.LogInformation("Falling back from {Plugin}", plugin.Id);
        }

        var final = lastResult ?? new DeliveryResult { Success = false, ErrorCode = "MAX_RETRIES", ErrorMessage = "All providers failed" };
        await FinalizeAsync(request, final, ct);
        return final;
    }

    public Task<DeliveryResult> SendAsync(NotificationRequest request, CancellationToken ct = default)
        => ProcessAsync(request, ct);

    private async Task FinalizeAsync(NotificationRequest request, DeliveryResult result, CancellationToken ct)
    {
        var status = result.Success ? DeliveryStatus.Sent :
            result.AttemptNumber >= MaxRetries ? DeliveryStatus.DeadLetter : DeliveryStatus.Failed;

        await _statusStore.UpdateStatusAsync(request.Id, status,
            providerMessageId: result.ProviderMessageId,
            errorCode: result.ErrorCode,
            errorMessage: result.ErrorMessage,
            attemptCount: result.AttemptNumber, ct: ct);

        await _audit.LogAsync(result.Success ? "sent" : "failed", request.Id, request.TenantId,
            details: result.Success ? result.ProviderId : $"{result.ErrorCode}: {result.ErrorMessage}", ct: ct);

        await _webhooks.DispatchAsync(result.Success ? "sent" : "failed", new
        {
            notificationId = request.Id,
            channel = ResolveChannel(request),
            recipient = request.Recipient,
            success = result.Success,
            providerId = result.ProviderId,
            providerMessageId = result.ProviderMessageId,
            error = result.ErrorMessage
        }, request.TenantId, ct);
    }

    private static string ResolveChannel(NotificationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Channel)) return request.Channel!;
        if (request.Channels is { Length: > 0 }) return request.Channels[0];
        return "email";
    }

}
