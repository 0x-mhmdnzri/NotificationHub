# ADR 0003: RabbitMQ as the Notification Work Queue

## Status

Accepted

## Date

2026-08-25

## Context

Sending notifications is I/O bound and failure-prone. The API must accept requests quickly, while actual provider delivery happens asynchronously with retries and status transitions.

Requirements:

- Durable queued work
- Decouple API latency from provider latency
- Support background workers and scheduled notification promotion
- Operate cleanly in Docker Compose

In-memory channels are insufficient for multi-instance deployment and process restarts.

## Decision

We will use **RabbitMQ** as the notification work queue.

- API enqueues accepted notifications.
- `NotificationBackgroundWorker` consumes and processes them.
- Queue/exchange are durable.
- Prefetch is configurable to control worker pressure.
- Scheduled notifications are stored in PostgreSQL and promoted to RabbitMQ by a scheduler worker when due.

## Alternatives Considered

### Option A: In-memory queue only
- **Pros:**
  - Zero infrastructure
  - Lowest implementation cost
- **Cons:**
  - Lost on process restart
  - Not shared across instances
- **Why rejected:**
  - Not production-safe for notification delivery guarantees.

### Option B: Redis lists / streams
- **Pros:**
  - Lightweight and fast
  - Already common in many stacks
- **Cons:**
  - Weaker built-in routing/management UX than RabbitMQ for this use case
  - Durability and ack semantics need more custom care
- **Why rejected:**
  - RabbitMQ provides clearer operational primitives for durable work queues and consumer ack patterns.

### Option C: Azure Service Bus / cloud-native queue
- **Pros:**
  - Managed service, less ops burden
- **Cons:**
  - Cloud lock-in and cost
  - Less portable for self-hosted / Iran-friendly deployment assumptions
- **Why rejected:**
  - Portability and self-host control are preferred at this stage.

## Consequences

**Positive:**
- API remains responsive under provider lag
- Durable asynchronous processing
- Clean separation between acceptance and delivery

**Negative / trade-offs:**
- Another infrastructure dependency
- Need dead-letter operational discipline and monitoring
- Local dev requires RabbitMQ (or Compose)

**Risks / follow-up actions:**
- Define explicit DLQ topology and poison-message handling beyond status flags
- Add metrics for queue depth, consumer lag, and retry rate
- Validate prefetch settings under production-like load

## References

- Related ADRs:
  - ADR 0001: Microkernel architecture
  - ADR 0002: PostgreSQL + PgBouncer
- Design docs:
  - `src/NotificationHub.Core/Queue/RabbitMqNotificationQueue.cs`
  - `docker-compose.yml`
- Discussion threads / tickets:
  - Phase 1 async processing decision (2026-08-25)
