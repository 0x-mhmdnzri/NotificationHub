# ADR 0017: Runtime Performance (Cold-start, JIT/PGO, GC)

## Status
Accepted

## Context
Multi-csproj Host (plugins, EF Core, Hangfire, MediatR, RabbitMQ) pays cold-start cost from JIT, assembly load, and first-allocation. Serverless/container deploys amplify p99 when cold-start + GC pause combine.

## Decision

### What we enable (safe for this architecture)

| Lever | Setting | Why |
|-------|---------|-----|
| ReadyToRun | `PublishReadyToRun=true` | Precompiled IL→native at publish; ~30–60% cold-start without breaking plugins |
| Tiered Compilation | default + explicit | Quick JIT then optimize hot methods |
| Dynamic PGO | `TieredPGO=true`, `DOTNET_TieredPGO=1` | Profile-guided optimization of hot paths after warm-up |
| Server GC + Concurrent | Host only | Throughput for multi-core API/workers |
| DATAS (.NET 9) | `DOTNET_GCDynamicAdaptationMode=1` | Adaptive heap count/size; avoid over-allocation |
| Trimming | **off** for Host | Plugins + EF + Hangfire need reflection |
| Native AOT | **deferred** | Plugin load, EF, Hangfire, MediatR not AOT-safe without major rewrite |

### What we do not do yet
- `PublishAot=true` on Host
- `PublishTrimmed=true` / full trim without AOT analyzers + source generators for every plugin
- Workstation GC in production (only experiment if p99 pause dominates and cores are few)
- Static PGO (mibc) until we have a stable load-test pipeline producing training data

### Measurement
- Script: `scripts/measure-cold-start.sh` — process start → first `/health/live`
- Record p50/p95/p99 before accepting further changes
- GC: EventPipe / `dotnet-trace` collect gc + JIT events under load

### Runtime env (containers / Aspire)
```
DOTNET_TieredPGO=1
DOTNET_TC_QuickJitForLoops=1
DOTNET_GCDynamicAdaptationMode=1
```

## Consequences
+ Lower cold-start and better steady-state hot paths with low code risk  
− Larger publish output with R2R  
− AOT still unavailable until plugin boundary is source-generated / trimmed  

## Follow-ups
1. Baseline cold-start numbers in CI artifact  
2. Reduce hot-path allocations (ArrayPool, avoid string concat on enqueue)  
3. Revisit partial trim + AOT for isolated worker processes without plugin reflection
