using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Messaging;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Store;

namespace NotificationHub.Core.Queue;

public sealed class NotificationBackgroundWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IServiceProvider _root;
    private readonly ILogger<NotificationBackgroundWorker> _logger;

    public NotificationBackgroundWorker(
        IServiceScopeFactory scopeFactory,
        IServiceProvider root,
        ILogger<NotificationBackgroundWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _root = root;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification background worker started");

        var rabbit = _root.GetService<RabbitMqNotificationQueue>();
        if (rabbit is not null)
        {
            var maxRedelivery = _root.GetService<IOptions<RabbitMqOptions>>()?.Value.MaxRedeliveryCount ?? 5;
            await foreach (var (request, deliveryTag, redelivery) in rabbit.DequeueWithAckAsync(stoppingToken))
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
                    if (!await inbox.TryMarkProcessedAsync(request.Id.ToString(), stoppingToken))
                    {
                        _logger.LogInformation("Duplicate message {Id} skipped (inbox)", request.Id);
                        await rabbit.AckAsync(deliveryTag, stoppingToken);
                        continue;
                    }

                    await ProcessOneAsync(scope, request, stoppingToken);
                    await rabbit.AckAsync(deliveryTag, stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process notification {Id}", request.Id);
                    if (redelivery + 1 >= maxRedelivery)
                        await rabbit.NackAsync(deliveryTag, requeue: false, stoppingToken);
                    else
                        await rabbit.NackAsync(deliveryTag, requeue: true, stoppingToken);
                }
            }
            return;
        }

        // In-memory fallback
        using var memScope = _scopeFactory.CreateScope();
        var queue = memScope.ServiceProvider.GetRequiredService<INotificationQueue>();
        await foreach (var request in queue.DequeueAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var inbox = scope.ServiceProvider.GetRequiredService<IInbox>();
                if (!await inbox.TryMarkProcessedAsync(request.Id.ToString(), stoppingToken))
                    continue;
                await ProcessOneAsync(scope, request, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process notification {Id}", request.Id);
            }
        }
    }

    private static async Task ProcessOneAsync(IServiceScope scope, NotificationRequest request, CancellationToken ct)
    {
        var orchestrator = scope.ServiceProvider.GetRequiredService<NotificationOrchestrator>();
        var statusStore = scope.ServiceProvider.GetRequiredService<INotificationStatusStore>();

        await statusStore.UpdateStatusAsync(request.Id, DeliveryStatus.Processing, ct: ct);
        var result = await orchestrator.ProcessAsync(request, ct);

        if (result.Success)
        {
            await statusStore.UpdateStatusAsync(request.Id, DeliveryStatus.Sent,
                providerMessageId: result.ProviderMessageId,
                attemptCount: result.AttemptNumber,
                ct: ct);
        }
        else
        {
            var finalStatus = result.AttemptNumber >= 3 ? DeliveryStatus.DeadLetter : DeliveryStatus.Failed;
            await statusStore.UpdateStatusAsync(request.Id, finalStatus,
                errorCode: result.ErrorCode,
                errorMessage: result.ErrorMessage,
                attemptCount: result.AttemptNumber,
                ct: ct);
        }
    }
}
