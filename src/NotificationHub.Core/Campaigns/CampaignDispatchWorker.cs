using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace NotificationHub.Core.Campaigns;

public sealed class CampaignDispatchOptions
{
    public const string SectionName = "Campaigns";
    public int BatchSize { get; set; } = 250;
    public int PollIntervalMs { get; set; } = 500;
    public int BusyPollIntervalMs { get; set; } = 0;
    /// <summary>Parallel AcceptAsync degree for claimed recipients (each uses own scope).</summary>
    public int AcceptConcurrency { get; set; } = 16;
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

        logger.LogInformation(
            "CampaignDispatchWorker started batch={Batch} acceptConcurrency={Conc}",
            opt.BatchSize, opt.AcceptConcurrency);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var campaigns = scope.ServiceProvider.GetRequiredService<ICampaignService>();
                var n = await campaigns.ProcessPendingBatchAsync(opt.BatchSize, stoppingToken);
                var delay = n > 0
                    ? Math.Max(0, opt.BusyPollIntervalMs)
                    : Math.Max(50, opt.PollIntervalMs);
                if (delay > 0)
                    await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "CampaignDispatchWorker iteration failed");
                await Task.Delay(Math.Max(50, options.Value.PollIntervalMs), stoppingToken);
            }
        }
    }
}
