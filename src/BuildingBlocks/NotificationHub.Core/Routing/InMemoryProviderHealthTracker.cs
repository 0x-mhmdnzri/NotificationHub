using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Routing;

public sealed class InMemoryProviderHealthTracker : IProviderHealthTracker
{
    private readonly ConcurrentDictionary<string, ProviderStats> _stats = new(StringComparer.OrdinalIgnoreCase);
    private readonly ProviderHealthOptions _options;

    public InMemoryProviderHealthTracker(IOptions<ProviderHealthOptions> options)
        => _options = options.Value;

    public void RecordSuccess(string providerId, string channel)
    {
        var stats = _stats.GetOrAdd(Key(providerId, channel), _ => new ProviderStats(providerId, channel, _options.WindowSize));
        lock (stats.Sync)
        {
            stats.Push(true, null);
            stats.LastSuccessAt = DateTimeOffset.UtcNow;
        }
    }

    public void RecordFailure(string providerId, string channel, string? errorCode = null)
    {
        var stats = _stats.GetOrAdd(Key(providerId, channel), _ => new ProviderStats(providerId, channel, _options.WindowSize));
        lock (stats.Sync)
        {
            stats.Push(false, errorCode);
            stats.LastFailureAt = DateTimeOffset.UtcNow;
            stats.LastErrorCode = errorCode;
        }
    }

    public ProviderHealthSnapshot GetHealth(string providerId, string channel)
    {
        if (!_stats.TryGetValue(Key(providerId, channel), out var stats))
        {
            return new ProviderHealthSnapshot
            {
                ProviderId = providerId,
                Channel = channel,
                SuccessRate = 1,
                TotalSamples = 0,
                IsHealthy = true
            };
        }

        lock (stats.Sync)
            return stats.ToSnapshot(_options);
    }

    public IReadOnlyList<ProviderHealthSnapshot> GetAll()
        => _stats.Values.Select(s =>
        {
            lock (s.Sync)
                return s.ToSnapshot(_options);
        }).OrderBy(x => x.Channel).ThenBy(x => x.ProviderId).ToList();

    private static string Key(string providerId, string channel) => $"{channel}:{providerId}";

    private sealed class ProviderStats
    {
        public object Sync { get; } = new();
        public string ProviderId { get; }
        public string Channel { get; }
        public DateTimeOffset? LastSuccessAt { get; set; }
        public DateTimeOffset? LastFailureAt { get; set; }
        public string? LastErrorCode { get; set; }
        private readonly Queue<bool> _window;
        private readonly int _windowSize;

        public ProviderStats(string providerId, string channel, int windowSize)
        {
            ProviderId = providerId;
            Channel = channel;
            _windowSize = Math.Max(5, windowSize);
            _window = new Queue<bool>(_windowSize);
        }

        public void Push(bool success, string? errorCode)
        {
            if (_window.Count >= _windowSize)
                _window.Dequeue();
            _window.Enqueue(success);
            if (!success)
                LastErrorCode = errorCode;
        }

        public ProviderHealthSnapshot ToSnapshot(ProviderHealthOptions options)
        {
            var total = _window.Count;
            var success = _window.Count(x => x);
            var rate = total == 0 ? 1.0 : (double)success / total;
            var healthy = total < options.MinSamples || rate >= options.UnhealthyThreshold;
            return new ProviderHealthSnapshot
            {
                ProviderId = ProviderId,
                Channel = Channel,
                SuccessRate = Math.Round(rate, 4),
                TotalSamples = total,
                SuccessCount = success,
                FailureCount = total - success,
                IsHealthy = healthy,
                LastSuccessAt = LastSuccessAt,
                LastFailureAt = LastFailureAt,
                LastErrorCode = LastErrorCode
            };
        }
    }
}
