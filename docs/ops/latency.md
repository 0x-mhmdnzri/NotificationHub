# Latency engineering (NotificationHub Accept path)

## Where latency lived
Accept path previously ran **serial** preference → consent → status → outbox → audit DB RTTs.

## What we changed (reduce vs hide)

| Change | Type | Effect |
|--------|------|--------|
| Preference cache before consent | Reduce | Cache hit skips preference DB RTT |
| Preference `IMemoryCache` (30s TTL) | Hide / reduce | Repeated recipients avoid DB |
| EF compiled queries (idempotency / collapse) | Reduce | Lower query compilation cost |
| `AddDbContextPool` + shorter command timeout | Reduce | Pool reuse, fail-fast |
| ConfigureAwait(false) on Accept awaits | Reduce | Avoid sync-context stalls |
| Response compression | Hide bandwidth | Smaller JSON for list endpoints |

## Measure

### Micro-benchmark (in-process)
```bash
dotnet run -c Release --project tests/NotificationHub.Benchmarks
```

### Stress / distribution (against running Host)
```bash
./scripts/loadtest.sh
# or
dotnet run -c Release --project tools/loadtest -- --total 2000 --concurrency 100 --warmup 50
```
Reports **p50/p90/p95/p99**, throughput, histogram — never a single average alone.

## Interpretation
- Rising **p99** with stable **p50** → tail (GC, pool exhaustion, provider/DB stragglers).
- Rising **p50** → systematic path cost (add parallelization / cache hits).
