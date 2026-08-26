using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NotificationHub.Core.Campaigns;

public sealed class CampaignDispatchOptions
{
    public const string SectionName = "Campaigns";
    public int BatchSize { get; set; } = 100;
    public int PollIntervalMs { get; set; } = 2000;
    public bool Enabled { get; set; } = true;
}

public sealed class CampaignDispatchWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<CampaignDispatchOptions> options,
    ILogger<CampaignDispatchWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opt = options.Value;
        if (!opt.Enabled)
        {
            logger.LogInformation("CampaignDispatchWorker disabled");
            return;
        }

        logger.LogInformation("CampaignDispatchWorker started batch={Batch}", opt.BatchSize);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var campaigns = scope.ServiceProvider.GetRequiredService<ICampaignService>();
                var n = await campaigns.ProcessPendingBatchAsync(opt.BatchSize, stoppingToken);
                if (n == 0)
                    await Task.Delay(opt.PollIntervalMs, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "CampaignDispatchWorker iteration failed");
                await Task.Delay(opt.PollIntervalMs, stoppingToken);
            }
        }
    }
}
