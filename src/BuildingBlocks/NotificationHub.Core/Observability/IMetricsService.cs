namespace NotificationHub.Core.Observability;

public interface IMetricsService
{
    void Increment(string name, double value = 1, params (string Key, string Value)[] tags);
    void Observe(string name, double value, params (string Key, string Value)[] tags);
    MetricsSnapshot Snapshot();
}

public sealed record MetricsSnapshot(
    IReadOnlyDictionary<string, double> Counters,
    IReadOnlyDictionary<string, MetricSummary> Observations);

public sealed record MetricSummary(long Count, double Sum, double Min, double Max, double Avg);
