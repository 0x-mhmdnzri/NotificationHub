# ADR 0018: High-Load Optimization (Cold-start · Runtime · Memory · Resources)

## Status
Accepted

## Context
NotificationHub is a multi-csproj, plugin-based messaging service. Under burst traffic the critical dimensions are:
1. **Cold-start** (container scale-out / first request)
2. **Steady-state runtime** (JIT quality on hot paths)
3. **Memory allocation** (GC pauses, LOH pressure on large JSON bodies)
4. **Resource management** (bounded queues, Kestrel limits, thread pool)

Native AOT and full trimming remain deferred: Host loads plugins via reflection, uses EF Core and Hangfire.

## Decision

### Cold-start
| Control | Value |
|---------|--------|
| PublishReadyToRun | true (Release) |
| PublishTrimmed / PublishAot | false on Host |
| TieredPGO | true + `DOTNET_TieredPGO=1` |
| Satellite cultures | `en` only |

Measure with `scripts/measure-cold-start.sh` before claiming improvement.

### Runtime (JIT)
- Tiered Compilation (default) + Dynamic PGO
- Hot path: RabbitMQ consumer deserializes **UTF-8 body span** (no `GetString`)
- Publish uses `SerializeToUtf8Bytes` (no intermediate UTF-16 JSON string)

### Memory allocation
- Prefer span/UTF-8 APIs on queue hot path
- `Utf8JsonBuffer` helper for rent-backed serialize when payload size is large
- Server GC + Concurrent; DATAS is default on .NET 9 (do not disable without measurement)
- Avoid unbounded in-memory buffers (bounded `Channel` already in workers)

### Resource management
- `HighLoad:*` configuration: Kestrel max body, keep-alive, optional min thread-pool
- RabbitMQ: prefetch vs `SemaphoreSlim` separation (ADR-006)
- Docker: `DOTNET_gcServer=1`, concurrent GC, optional `GCHeapHardLimitPercent` for containers

## Non-goals
- Permanent `GCLatencyMode.SustainedLowLatency` (only opt-in via config for short windows)
- Raising LOH threshold without allocation-rate proof
- AOT until plugin contract is source-generated / trim-safe

## Consequences
+ Lower allocations per consumed message; clearer production knobs  
− R2R increases publish artifact size  
− Operators must still measure (allocation rate, % Time in GC, p99) before further GC knobs  

## Follow-up measurements (Step 0 of skill)
1. Cold-start p50/p95 to `/health/live`
2. Under load: allocation rate, Gen0/1/2 collections, LOH size
3. After code changes: same harness — reject regressions in steady-state latency
