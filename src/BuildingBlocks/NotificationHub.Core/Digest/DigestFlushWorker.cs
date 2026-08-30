using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace NotificationHub.Core.Digest;

public sealed class DigestFlushWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DigestFlushWorker> _logger;

    public DigestFlushWorker(IServiceScopeFactory scopeFactory, ILogger<DigestFlushWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var digest = scope.ServiceProvider.GetRequiredService<IDigestService>();
                var n = await digest.FlushDueAsync(stoppingToken);
                if (n > 0)
                    _logger.LogInformation("Digest worker flushed {Count} buffer rows", n);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Digest flush worker error");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
