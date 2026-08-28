# ADR 0006: RabbitMQ Worker Management Algorithm

## Status
Accepted

## Context
Notification delivery is **I/O-bound** (HTTP to SendGrid, Twilio, FCM, etc.) with **highly variable** processing time (10ms–several seconds). The previous worker:

1. Used `Channel.CreateUnbounded` — moved queue pressure into process RAM
2. Processed messages **sequentially** despite `PrefetchCount = 10`
3. Did not separate **RabbitMQ delivery flow control** (prefetch) from **application concurrency**

This under-utilized capacity and risked memory growth under load.

## Decision

Adopt:

```text
Competing Consumers
  + Fair Dispatch (basic.qos prefetch)
  + Application Worker Pool (SemaphoreSlim)
  + Bounded hand-off buffer
  + At-least-once + Inbox idempotency
  + Delayed retry + DLQ
```

### Why not pure Round Robin alone?
Round Robin assigns messages evenly but does **not** equalize work when job duration varies. Fair dispatch via low/moderate prefetch + ACK-gated delivery is required.

### Why not unbounded internal concurrency?
Unlimited `Task` fan-out would overwhelm DB connection pools and provider rate limits.

### Parameter defaults

| Knob | Layer | Default | Rationale |
|------|--------|---------|-----------|
| `PrefetchCount` | RabbitMQ QoS | 16 | Fairness; limits unacked work per consumer |
| `WorkerMaxConcurrency` | Application | 8 | I/O parallelism; ≤ prefetch so work stays in broker when saturated |
| `ConsumerBufferCapacity` | Hand-off | 32 | Bounded backpressure; no unbounded RAM queue |
| `MaxRedeliveryCount` | Retry | 5 | Bounded poison protection → DLQ |

### ACK serialization
`IChannel` is **not** thread-safe for concurrent `basic.ack`. Concurrent workers share one channel; ACK/NACK is serialized with a dedicated `SemaphoreSlim`.

### Horizontal scale
Deploy multiple Host instances (competing consumers on the same queue). Prefer scale-out for throughput; tune concurrency per instance for downstream capacity.

### Ordering
Global order is **not** guaranteed with concurrent workers. Per-aggregate ordering requires partition-by-key topology (future ADR if product requires it). Channel routing (`ChannelRouting`) isolates channel queues when enabled.

## Consequences

**Positive**
- Higher throughput on I/O-bound delivery
- Fairer distribution under variable latency
- Explicit backpressure (bounded buffer + prefetch)
- Clear operational knobs

**Negative / costs**
- Slightly more complex worker code
- ACK serialization can become a bottleneck at extreme concurrency (mitigate with multiple channels/consumers later)

**Rejected alternatives**
- Prefetch=1 only: too slow for this I/O profile
- Prefetch=10000: hoarding, unfairness, memory
- ACK-before-process: message loss
- Infinite requeue: poison loops

## Observability
Track: queue depth, oldest message age, unacked, processing latency, success/fail/retry/DLQ rates, worker concurrency utilization.

## Related
- ADR-005 Domain-Driven Design
- `rabbitmq.skill` / transactional outbox
- `NotificationBackgroundWorker`, `RabbitMqOptions`
