using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Routing;

public sealed class CircuitBreakerOptions
{
    public const string SectionName = "CircuitBreaker";
    /// <summary>Consecutive failures to open circuit.</summary>
    public int FailureThreshold { get; set; } = 5;
    /// <summary>Seconds to stay open before half-open probe.</summary>
    public int OpenDurationSeconds { get; set; } = 60;
}

public enum CircuitState { Closed, Open, HalfOpen }

/// <summary>F19 — formal circuit breaker on top of sliding-window health.</summary>
public sealed class CircuitBreakerProviderHealthTracker : IProviderHealthTracker
{
    private readonly InMemoryProviderHealthTracker _inner;
    private readonly CircuitBreakerOptions _options;
    private readonly Dictionary<string, CircuitEntry> _circuits = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _sync = new();

    public CircuitBreakerProviderHealthTracker(IOptions<ProviderHealthOptions> healthOptions, IOptions<CircuitBreakerOptions>? cbOptions = null)
    {
        _inner = new InMemoryProviderHealthTracker(healthOptions);
        _options = cbOptions?.Value ?? new CircuitBreakerOptions();
    }

    public void RecordSuccess(string providerId, string channel)
    {
        _inner.RecordSuccess(providerId, channel);
        lock (_sync)
        {
            var e = GetOrCreate(providerId, channel);
            e.ConsecutiveFailures = 0;
            e.State = CircuitState.Closed;
            e.OpenedAt = null;
        }
    }

    public void RecordFailure(string providerId, string channel, string? errorCode = null)
    {
        _inner.RecordFailure(providerId, channel, errorCode);
        lock (_sync)
        {
            var e = GetOrCreate(providerId, channel);
            e.ConsecutiveFailures++;
            e.LastError = errorCode;
            if (e.State == CircuitState.HalfOpen || e.ConsecutiveFailures >= _options.FailureThreshold)
            {
                e.State = CircuitState.Open;
                e.OpenedAt = DateTimeOffset.UtcNow;
            }
        }
    }

    public ProviderHealthSnapshot GetHealth(string providerId, string channel)
    {
        var snap = _inner.GetHealth(providerId, channel);
        lock (_sync)
        {
            var e = GetOrCreate(providerId, channel);
            Transition(e);
            if (e.State == CircuitState.Open)
            {
                return snap with { IsHealthy = false, LastErrorCode = e.LastError ?? "CIRCUIT_OPEN" };
            }
            if (e.State == CircuitState.HalfOpen)
            {
                // allow probe: report healthy so router may try once
                return snap with { IsHealthy = true };
            }
            return snap;
        }
    }

    public IReadOnlyList<ProviderHealthSnapshot> GetAll()
    {
        var all = _inner.GetAll().ToList();
        lock (_sync)
        {
            return all.Select(s =>
            {
                var e = GetOrCreate(s.ProviderId, s.Channel);
                Transition(e);
                return e.State == CircuitState.Open
                    ? s with { IsHealthy = false, LastErrorCode = e.LastError ?? "CIRCUIT_OPEN" }
                    : s;
            }).ToList();
        }
    }

    /// <summary>Test/inspection helper.</summary>
    public CircuitState GetCircuitState(string providerId, string channel)
    {
        lock (_sync)
        {
            var e = GetOrCreate(providerId, channel);
            Transition(e);
            return e.State;
        }
    }

    private void Transition(CircuitEntry e)
    {
        if (e.State == CircuitState.Open && e.OpenedAt is not null
            && DateTimeOffset.UtcNow - e.OpenedAt >= TimeSpan.FromSeconds(_options.OpenDurationSeconds))
        {
            e.State = CircuitState.HalfOpen;
        }
    }

    private CircuitEntry GetOrCreate(string providerId, string channel)
    {
        var key = $"{channel}:{providerId}";
        if (!_circuits.TryGetValue(key, out var e))
        {
            e = new CircuitEntry();
            _circuits[key] = e;
        }
        return e;
    }

    private sealed class CircuitEntry
    {
        public CircuitState State { get; set; } = CircuitState.Closed;
        public int ConsecutiveFailures { get; set; }
        public DateTimeOffset? OpenedAt { get; set; }
        public string? LastError { get; set; }
    }
}
