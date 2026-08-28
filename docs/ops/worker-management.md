# Worker Management

## Algorithm

**Competing Consumers + Fair Dispatch + Bounded Application Concurrency**

```text
RabbitMQ Queue
      |  prefetch (basic.qos)
      v
Consumer callback
      |  bounded Channel (backpressure)
      v
Worker pool (SemaphoreSlim MaxConcurrency)
      |  process + inbox
      v
ACK / delayed retry / DLQ
```

## Configuration (`RabbitMQ` section)

| Key | Meaning |
|-----|---------|
| `PrefetchCount` | Max unacked deliveries per consumer (RabbitMQ) |
| `WorkerMaxConcurrency` | Max concurrent `ProcessOne` tasks (app) |
| `ConsumerBufferCapacity` | Bounded hand-off buffer size |
| `MaxRedeliveryCount` | Then DLQ via nack without requeue |
| `RetryDelaySeconds` | Delayed retry ladder |

**Rule:** Prefer `WorkerMaxConcurrency ≤ PrefetchCount`.

## Tuning guide

| Symptom | Action |
|---------|--------|
| High queue age, low CPU | Increase `WorkerMaxConcurrency` (watch provider limits) |
| Provider 429 / DB saturation | Decrease concurrency |
| Uneven worker load | Lower prefetch |
| Process memory growth | Ensure buffer is bounded; lower prefetch |
| Poison messages | Check DLQ; do not raise MaxRedelivery blindly |

## Graceful shutdown
In-flight tasks drain before exit. Unfinished work is redelivered by RabbitMQ if not ACKed.

## Horizontal scaling
Run multiple API/worker Host replicas on the same queue. Autoscale on **queue age** and processing rate, not queue depth alone.


## Outbox relay (enqueue latency)

| Key | Default | Notes |
|-----|---------|-------|
| `OutboxRelay:IdlePollIntervalMs` | 250 | Empty queue back-off |
| `OutboxRelay:BusyPollIntervalMs` | 0 | Keep draining under load |
| `OutboxRelay:BatchSize` | 100 | SKIP LOCKED claim size |
| `OutboxRelay:PublishConcurrency` | 16 | Parallel prepare; publish serialized on channel |

## Campaign accept concurrency

| Key | Default |
|-----|---------|
| `Campaigns:AcceptConcurrency` | 16 |
| `Campaigns:BatchSize` | 250 |
| `Campaigns:BusyPollIntervalMs` | 0 |

Each parallel Accept uses its own DI scope (EF DbContext is not thread-safe).
