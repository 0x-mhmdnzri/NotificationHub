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
