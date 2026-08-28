using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Queue;
using NotificationHub.Core.Store;

namespace NotificationHub.Core.Scheduling;

public sealed class ScheduledNotificationWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledNotificationWorker> _logger;

    public ScheduledNotificationWorker(IServiceScopeFactory scopeFactory, ILogger<ScheduledNotificationWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduler worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<INotificationStatusStore>();
                var queue = scope.ServiceProvider.GetRequiredService<INotificationQueue>();

                var due = await store.GetDueScheduledAsync(DateTimeOffset.UtcNow, 50, stoppingToken);
                foreach (var item in due)
                {
                    if (string.IsNullOrEmpty(item.PayloadJson)) continue;
                    var request = JsonSerializer.Deserialize<NotificationRequest>(item.PayloadJson);
                    if (request is null) continue;

                    await store.UpdateStatusAsync(item.Id, DeliveryStatus.Queued, ct: stoppingToken);
                    await queue.EnqueueAsync(request, stoppingToken);
                    _logger.LogInformation("Scheduled notification {Id} moved to queue", item.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduler tick failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
