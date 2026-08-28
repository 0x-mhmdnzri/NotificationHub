# ADR 0007: Latency Reduction & Controlled Concurrency

## Status
Accepted

## Context
Under traffic spikes, end-to-end latency was dominated by **sequential workers** and a **fixed 2s outbox poll**:

```text
Accept → Outbox (pending) → [wait up to 2s] → RabbitMQ → [serial consumer] → Provider
```

Bottlenecks measured conceptually:

| Stage | Problem |
|-------|---------|
| OutboxRelayWorker | `Task.Delay(2s)` even when queue had work |
| Outbox publish | Sequential `foreach` publish |
| NotificationBackgroundWorker | Fixed (improved in ADR-006) |
| CampaignDispatch | Sequential Accept per recipient; poll always delayed |

Concurrency skill constraints:

- `DbContext` is **not** thread-safe → parallel Accept needs **own scope**
- RabbitMQ `IChannel` is **not** multi-thread safe → publish/ack **serialized**
- Unbounded task fan-out is an anti-pattern → `Parallel.ForEachAsync` + `MaxDegreeOfParallelism` / `SemaphoreSlim`

## Decision

### 1. Adaptive outbox polling
- `BusyPollIntervalMs = 0` when a batch was claimed (keep draining)
- `IdlePollIntervalMs = 250` when empty (not 2000)
- Larger `BatchSize` (100) with `SKIP LOCKED`

### 2. Parallel outbox prepare/publish
- `Parallel.ForEachAsync` with `PublishConcurrency` (16)
- JSON deserialize parallel; `BasicPublish` under `_publishGate` (channel safety)

### 3. Parallel campaign Accept
- Claim recipients on one context
- `Parallel.ForEachAsync` + **CreateAsyncScope** per Accept
- Merge results onto tracked entities → single `SaveChanges`

### 4. Higher delivery worker defaults
- Prefetch 32, WorkerMaxConcurrency 16, buffer 64
- Still `concurrency ≤ prefetch` guidance

## Why not unbounded parallelism?
Provider rate limits, Postgres connection pool, and GC would collapse under `Task.WhenAll` of 10k Accepts. Bounded concurrency preserves latency **and** stability.

## Why not lower idle poll to 0 always?
Busy-wait burns CPU when idle. 250ms idle is a latency/CPU trade-off; busy path is zero-delay.

## Consequences

**Latency**
- Enqueue→Rabbit under load: from ≤2s+publish to ~batch claim + parallel publish
- Campaign fan-out: throughput scales with `AcceptConcurrency`

**Risks**
- Higher DB connection use under parallel Accept → size pool accordingly
- Channel publish lock can serialize extreme publish rates → future: multi-channel publishers

**Observability**
Track: outbox lag (CreatedAt→PublishedAt), queue age, accept concurrency utilization, provider latency p95/p99.

## Related
- ADR-006 RabbitMQ Worker Management
- C# concurrency skill: SemaphoreSlim, Parallel.ForEachAsync, no lock across I/O
