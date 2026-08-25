using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Orchestration;
using NotificationHub.Core.Store;

namespace NotificationHub.Core.Queue;

public sealed class NotificationBackgroundWorker : BackgroundService
{
    private readonly INotificationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<NotificationBackgroundWorker> _logger;

    public NotificationBackgroundWorker(
        INotificationQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<NotificationBackgroundWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Notification background worker started");

        await foreach (var request in _queue.DequeueAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<NotificationOrchestrator>();
                var statusStore = scope.ServiceProvider.GetRequiredService<INotificationStatusStore>();

                await statusStore.UpdateStatusAsync(request.Id, DeliveryStatus.Processing, ct: stoppingToken);
                var result = await orchestrator.ProcessAsync(request, stoppingToken);

                if (result.Success)
                {
                    await statusStore.UpdateStatusAsync(request.Id, DeliveryStatus.Sent,
                        providerMessageId: result.ProviderMessageId,
                        attemptCount: result.AttemptNumber,
                        ct: stoppingToken);
                }
                else
                {
                    // Retry logic is inside orchestrator; if it returns failed after retries, mark as Failed or DeadLetter
                    var finalStatus = result.AttemptNumber >= 3 ? DeliveryStatus.DeadLetter : DeliveryStatus.Failed;
                    await statusStore.UpdateStatusAsync(request.Id, finalStatus,
                        errorCode: result.ErrorCode,
                        errorMessage: result.ErrorMessage,
                        attemptCount: result.AttemptNumber,
                        ct: stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process notification {Id}", request.Id);
            }
        }
    }
}
