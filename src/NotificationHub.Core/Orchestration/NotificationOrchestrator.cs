using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Audit;
using NotificationHub.Core.Compliance;
using NotificationHub.Core.Messaging;
using NotificationHub.Core.PluginHost;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Routing;
using NotificationHub.Core.Store;
using NotificationHub.Core.Templates;
using NotificationHub.Core.Webhooks;

namespace NotificationHub.Core.Orchestration;

public sealed class NotificationOrchestrator
{
    private readonly PluginLoader _pluginLoader;
    private readonly ITemplateEngine _templateEngine;
    private readonly INotificationStatusStore _statusStore;
    private readonly IPreferenceService _preferences;
    private readonly IConsentService _consents;
    private readonly IOutbox _outbox;
    private readonly IAuditService _audit;
    private readonly IWebhookDispatcher _webhooks;
    private readonly IProviderRouter _router;
    private readonly IProviderHealthTracker _health;
    private readonly ILogger<NotificationOrchestrator> _logger;
    private const int MaxRetries = 3;

    public NotificationOrchestrator(
        PluginLoader pluginLoader,
        ITemplateEngine templateEngine,
        INotificationStatusStore statusStore,
        IPreferenceService preferences,
        IConsentService consents,
        IOutbox outbox,
        IAuditService audit,
        IWebhookDispatcher webhooks,
        IProviderRouter router,
        IProviderHealthTracker health,
        ILogger<NotificationOrchestrator> logger)
    {
        _pluginLoader = pluginLoader;
        _templateEngine = templateEngine;
        _statusStore = statusStore;
        _preferences = preferences;
        _consents = consents;
        _outbox = outbox;
        _audit = audit;
        _webhooks = webhooks;
        _router = router;
        _health = health;
        _logger = logger;
    }

    public async Task<(bool Accepted, NotificationStatus Status)> AcceptAsync(NotificationRequest request, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existing = await _statusStore.GetByIdempotencyKeyAsync(request.IdempotencyKey, request.TenantId, ct);
            if (existing is not null)
            {
                _logger.LogInformation("Idempotent hit {Key} -> {Id}", request.IdempotencyKey, existing.NotificationId);
                return (true, existing);
            }
        }

        var channel = ResolveChannel(request);

        // Preference check
        var (allowed, reason) = await _preferences.CanSendAsync(request.Recipient, channel, request.Category, request.TenantId, ct);
        if (!allowed)
        {
            var suppressed = new NotificationStatus
            {
                NotificationId = request.Id, Channel = channel, Recipient = request.Recipient,
                Status = DeliveryStatus.Suppressed, TenantId = request.TenantId,
                IdempotencyKey = request.IdempotencyKey, CorrelationId = request.CorrelationId,
                Category = request.Category, ErrorMessage = reason
            };
            await _statusStore.SaveAsync(suppressed, ct);
            await _audit.LogAsync("suppressed", request.Id, request.TenantId, details: reason, ct: ct);
            return (true, suppressed);
        }

        // Consent ledger (purpose = category or transactional default)
        var purpose = string.IsNullOrWhiteSpace(request.Category) ? "transactional" : request.Category;
        var consent = await _consents.EvaluateAsync(request.Recipient, purpose, channel, request.TenantId, ct);
        if (!consent.Allowed)
        {
            var suppressed = new NotificationStatus
            {
                NotificationId = request.Id, Channel = channel, Recipient = request.Recipient,
                Status = DeliveryStatus.Suppressed, TenantId = request.TenantId,
                IdempotencyKey = request.IdempotencyKey, CorrelationId = request.CorrelationId,
                Category = request.Category, ErrorMessage = consent.Reason
            };
            await _statusStore.SaveAsync(suppressed, ct);
            await _audit.LogAsync("suppressed", request.Id, request.TenantId, details: consent.Reason, ct: ct);
            return (true, suppressed);
        }

        var isScheduled = request.ScheduledAt.HasValue && request.ScheduledAt > DateTimeOffset.UtcNow;
        var status = new NotificationStatus
        {
            NotificationId = request.Id, Channel = channel, Recipient = request.Recipient,
            Status = isScheduled ? DeliveryStatus.Scheduled : DeliveryStatus.Queued,
            ScheduledAt = request.ScheduledAt, TenantId = request.TenantId,
            IdempotencyKey = request.IdempotencyKey, CorrelationId = request.CorrelationId,
            Category = request.Category
        };

        await _statusStore.SaveAsync(status, ct);
        await _statusStore.SavePayloadAsync(request.Id, JsonSerializer.Serialize(request), ct);
        await _audit.LogAsync(isScheduled ? "scheduled" : "queued", request.Id, request.TenantId, ct: ct);
        return (true, status);
    }

    public async Task<DeliveryResult> ProcessAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var channel = ResolveChannel(request);
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
