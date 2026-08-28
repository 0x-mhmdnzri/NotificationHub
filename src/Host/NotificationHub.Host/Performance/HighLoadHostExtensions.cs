using System.Runtime;

namespace NotificationHub.Host.Performance;

/// <summary>
/// Runtime resource knobs for high-load Host (skill: resource management layer).
/// All values are optional via configuration — defaults stay conservative.
/// </summary>
public static class HighLoadHostExtensions
{
    public static WebApplicationBuilder AddHighLoadRuntimeTuning(this WebApplicationBuilder builder)
    {
        builder.WebHost.ConfigureKestrel(options =>
        {
            var maxBody = builder.Configuration.GetValue("HighLoad:MaxRequestBodyBytes", 1024 * 1024);
            options.Limits.MaxRequestBodySize = maxBody;
            options.Limits.MaxConcurrentConnections =
                builder.Configuration.GetValue<long?>("HighLoad:MaxConcurrentConnections");
            options.Limits.MaxConcurrentUpgradedConnections =
                builder.Configuration.GetValue<long?>("HighLoad:MaxConcurrentUpgradedConnections");
            options.Limits.KeepAliveTimeout = TimeSpan.FromSeconds(
                builder.Configuration.GetValue("HighLoad:KeepAliveSeconds", 30));
            options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(
                builder.Configuration.GetValue("HighLoad:RequestHeadersTimeoutSeconds", 15));
        });

        var minWorker = builder.Configuration.GetValue<int?>("HighLoad:MinWorkerThreads");
        var minIo = builder.Configuration.GetValue<int?>("HighLoad:MinIoThreads");
        if (minWorker is > 0 || minIo is > 0)
        {
            ThreadPool.GetMinThreads(out var w, out var io);
            ThreadPool.SetMinThreads(minWorker ?? w, minIo ?? io);
        }

        if (builder.Configuration.GetValue("HighLoad:SustainedLowLatencyGc", false))
        {
            GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
        }

        return builder;
    }
}
