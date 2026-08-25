using System.Collections.Concurrent;

namespace NotificationHub.Core.Observability;

/// <summary>F29 — lightweight in-process metrics (exportable via admin API).</summary>
public sealed class InMemoryMetricsService : IMetricsService
{
    private readonly ConcurrentDictionary<string, double> _counters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, Obs> _obs = new(StringComparer.OrdinalIgnoreCase);

    public void Increment(string name, double value = 1, params (string Key, string Value)[] tags)
    {
        var key = Format(name, tags);
        _counters.AddOrUpdate(key, value, (_, cur) => cur + value);
    }

    public void Observe(string name, double value, params (string Key, string Value)[] tags)
    {
        var key = Format(name, tags);
        _obs.AddOrUpdate(key,
            _ => new Obs { Count = 1, Sum = value, Min = value, Max = value },
            (_, o) =>
            {
                o.Count++;
                o.Sum += value;
                if (value < o.Min) o.Min = value;
                if (value > o.Max) o.Max = value;
                return o;
            });
    }

    public MetricsSnapshot Snapshot()
    {
        var observations = _obs.ToDictionary(
            kv => kv.Key,
            kv => new MetricSummary(kv.Value.Count, kv.Value.Sum, kv.Value.Min, kv.Value.Max,
                kv.Value.Count == 0 ? 0 : kv.Value.Sum / kv.Value.Count),
            StringComparer.OrdinalIgnoreCase);
        return new MetricsSnapshot(new Dictionary<string, double>(_counters, StringComparer.OrdinalIgnoreCase), observations);
    }

    private static string Format(string name, (string Key, string Value)[] tags)
    {
        if (tags is null || tags.Length == 0) return name;
        return name + "{" + string.Join(",", tags.Select(t => $"{t.Key}={t.Value}")) + "}";
    }

    private sealed class Obs
    {
        public long Count;
        public double Sum, Min, Max;
    }
}
