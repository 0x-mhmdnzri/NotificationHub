using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.PluginHost;
using NotificationHub.Core.Store;
using NotificationHub.Core.Templates;

namespace NotificationHub.Core.Orchestration;

public sealed class NotificationOrchestrator
{
    private readonly PluginLoader _pluginLoader;
    private readonly ITemplateEngine _templateEngine;
    private readonly INotificationStatusStore _statusStore;
    private readonly ILogger<NotificationOrchestrator> _logger;
    private const int MaxRetries = 3;

    public NotificationOrchestrator(
        PluginLoader pluginLoader,
        ITemplateEngine templateEngine,
        INotificationStatusStore statusStore,
        ILogger<NotificationOrchestrator> logger)
    {
        _pluginLoader = pluginLoader;
        _templateEngine = templateEngine;
        _statusStore = statusStore;
        _logger = logger;
    }

    /// <summary>
    /// Enqueue path: called from API. Handles idempotency + status creation.
    /// </summary>
    public async Task<(bool Accepted, NotificationStatus Status)> AcceptAsync(NotificationRequest request, CancellationToken ct = default)
    {
        // Idempotency check
        if (!string.IsNullOrEmpty(request.IdempotencyKey))
        {
            var existing = await _statusStore.GetByIdempotencyKeyAsync(request.IdempotencyKey, request.TenantId, ct);
            if (existing is not null)
            {
                _logger.LogInformation("Idempotent hit for key {Key} -> {Id}", request.IdempotencyKey, existing.NotificationId);
                return (true, existing);
            }
        }

        var status = new NotificationStatus
        {
            NotificationId = request.Id,
            Channel = request.Channel,
            Recipient = request.Recipient,
            Status = request.ScheduledAt.HasValue && request.ScheduledAt > DateTimeOffset.UtcNow
                ? DeliveryStatus.Scheduled
                : DeliveryStatus.Queued,
            TenantId = request.TenantId,
            IdempotencyKey = request.IdempotencyKey,
            CorrelationId = request.CorrelationId,
            AttemptCount = 0
        };

        await _statusStore.SaveAsync(status, ct);
        return (true, status);
    }

    /// <summary>
    /// Actual processing with retry + exponential backoff.
    /// </summary>
    public async Task<DeliveryResult> ProcessAsync(NotificationRequest request, CancellationToken ct = default)
    {
        var channelPlugins = _pluginLoader.LoadedPlugins
            .OfType<IChannelPlugin>()
            .Where(p => p.Channel.Equals(request.Channel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (channelPlugins.Count == 0)
        {
            _logger.LogWarning("No plugin found for channel {Channel}", request.Channel);
            return new DeliveryResult
            {
                Success = false,
                ErrorCode = "NO_PLUGIN",
                ErrorMessage = $"No plugin registered for channel '{request.Channel}'",
                AttemptNumber = 1
            };
        }

        var plugin = channelPlugins[0]; // TODO: failover / smart routing later
        var rendered = await _templateEngine.RenderAsync(request, ct);

        DeliveryResult? lastResult = null;

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                _logger.LogInformation("Attempt {Attempt}/{Max} for notification {Id} via {Plugin}",
                    attempt, MaxRetries, request.Id, plugin.Id);

                var result = await plugin.SendAsync(rendered, ct);
                result = result with { AttemptNumber = attempt };

                if (result.Success)
                {
                    _logger.LogInformation("Notification {Id} sent successfully on attempt {Attempt}", request.Id, attempt);
                    return result;
                }

                lastResult = result;
                _logger.LogWarning("Attempt {Attempt} failed for {Id}: {Error}", attempt, request.Id, result.ErrorMessage);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception on attempt {Attempt} for {Id}", attempt, request.Id);
                lastResult = new DeliveryResult
                {
                    Success = false,
                    ErrorCode = "EXCEPTION",
                    ErrorMessage = ex.Message,
                    AttemptNumber = attempt
                };
            }

            if (attempt < MaxRetries)
            {
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt)); // exponential: 2s, 4s, 8s
                await Task.Delay(delay, ct);
            }
        }

        return lastResult ?? new DeliveryResult
        {
            Success = false,
            ErrorCode = "MAX_RETRIES",
            ErrorMessage = "Max retries exceeded",
            AttemptNumber = MaxRetries
        };
    }

    // Keep old SendAsync for sync path if needed
    public Task<DeliveryResult> SendAsync(NotificationRequest request, CancellationToken ct = default)
        => ProcessAsync(request, ct);
}
