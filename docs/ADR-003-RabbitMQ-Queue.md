# ADR 0003: RabbitMQ Work Queue with Transactional Outbox/Inbox

## Status

Accepted

## Date

2026-08-25

## Context

Sending notifications is I/O-bound and failure-prone. The API must accept work quickly while provider delivery happens asynchronously.

Reliability problems we must avoid (from messaging architecture practice):

- **Dual-write**: DB status committed but RabbitMQ publish fails (or the reverse) → lost or phantom work.
- **At-least-once delivery**: consumers may see duplicates after crash between process and ack.
- **Poison messages**: permanent failures looping forever without a dead-letter path.
- **Ack-before-process**: acknowledging before business work completes causes silent loss on crash.

Constraints:

- Self-hosted / Docker Compose friendly
- PostgreSQL already chosen for persistence
- Prefer explicit AMQP topology over opaque frameworks for the core path
- TransactionalBox NuGet was considered but is not production-declared and centers on Kafka transport; we need RabbitMQ

## Decision

We will use **RabbitMQ** as the notification transport, combined with a **PostgreSQL transactional outbox** on the publish path and an **inbox** on the consume path.

### Topology (baseline)
- Durable direct exchange `notifications.exchange`
- Durable work queue `notifications` with `x-dead-letter-exchange` → `notifications.dlx`
- Durable DLQ `notifications.dlq`
- Manual ack, configurable prefetch
- Persistent messages (`delivery_mode=2`)
- Header `x-redelivery-count` for poison control

### Publish path (Outbox)
1. API / orchestrator writes `notification_statuses` and `outbox_messages` (pending).
2. `OutboxRelayWorker` polls pending outbox rows and publishes to RabbitMQ.
3. On success marks outbox `published`; on failure applies exponential backoff and eventually `failed`.

### Consume path (Inbox + correct ack)
1. Worker receives message **without** auto-ack.
2. Inbox check — if already processed, ack and skip.
3. Process notification (providers, status updates).
4. **Ack only after successful handling**; on failure schedule delayed redelivery or nack without requeue (DLQ) after max attempts.

### API enqueue abstraction
- `INotificationQueue` for API callers is `OutboxNotificationQueue` (writes outbox only).
- `RabbitMqNotificationQueue` is the AMQP transport used by relay + worker.

## Amendment — Per-channel routing (2026-08-26)

**Status:** Accepted (see **ADR-013**)

When `RabbitMQ:ChannelRouting` is `true`:

- Work queues: `notifications.{channel}` (e.g. `notifications.email`, `notifications.sms`, `notifications.push`)
- Routing keys: `notification.send.{channel}`
- Retry TTL queues remain per work-queue prefix; DLX/DLQ shared
- Publish selects channel from the notification payload; consumers set `RabbitMQ:ConsumeChannel` to bind to one queue

When `ChannelRouting` is `false`, the baseline single-queue topology above still applies.

This amendment does **not** change outbox/inbox semantics—only broker partitioning for independent scale and isolation.

## Alternatives Considered

### Option A: Direct publish from API to RabbitMQ (no outbox)
- **Pros:**
  - Simpler, fewer moving parts
- **Cons:**
  - Dual-write between PostgreSQL status and broker
  - Lost notifications when publish fails after DB commit
- **Why rejected:**
  - Violates reliability requirements for notification delivery.

### Option B: TransactionalBox library
- **Pros:**
  - Ready-made outbox/inbox abstractions
- **Cons:**
  - Officially not production-ready
  - Transport focus is Kafka; RabbitMQ not a first-class documented path
- **Why rejected:**
  - Prefer a lean, explicit outbox over an immature dependency for our broker.

### Option C: MassTransit
- **Pros:**
  - Built-in retry, outbox, error queues
- **Cons:**
  - Heavier abstraction over microkernel-friendly explicit plugins
  - Larger operational surface for current phase
- **Why rejected:**
  - Deferred; may revisit when multi-message saga complexity grows.

### Option D: In-memory channel only
- **Pros:**
  - Zero infra
- **Cons:**
  - Lost on restart; not multi-instance
- **Why rejected:**
  - Unacceptable for production delivery guarantees.

## Consequences

**Positive:**
- Accept path is durable even if RabbitMQ is briefly down (outbox drains later)
- Consumers are idempotent under redelivery (inbox)
- Poison messages surface in DLQ instead of infinite retry loops
- Ack-after-process eliminates the classic loss window
- (Amendment) Channel isolation and independent consumer scale

**Negative / trade-offs:**
- Extra table + relay lag (seconds) before broker visibility
- Two write models to operate (outbox depth + queue depth)
- Inbox table grows and needs retention/cleanup
- (Amendment) More queues to monitor when channel routing is on

**Risks / follow-up actions:**
- ~~Alert on `outbox_messages` pending age and `notifications.dlq` depth~~ → `MessagingHealthMonitorWorker` + `GET /api/v1/admin/messaging/health`
- ~~Add publisher confirms for stronger publish durability~~ → `CreateChannelOptions(publisherConfirmationsEnabled: true)` + await confirm on publish
- ~~Retention job for old inbox/outbox rows~~ → `RetentionService` (`OutboxPublishedDays`, `InboxDays`)
- ~~Load-test prefetch vs provider RPS~~ → `tools/loadtest` + `docs/ops/prefetch-tuning.md`
- ~~Optional future: delayed redelivery exchange instead of requeue for backoff~~ → TTL retry queues (`*.retry.{N}s`) dead-letter back to work queue
- Optional future: MassTransit if multi-message sagas dominate
- Per-channel depth alerts when `ChannelRouting` is enabled (ADR-013)

### Implemented reliability controls (post-decision)
| Control | Mechanism |
|---------|-----------|
| Dual-write safety | PostgreSQL outbox + relay |
| Publish durability | Publisher confirms (tracked await) |
| Consumer idempotency | `inbox_messages` |
| Poison messages | DLX/DLQ + max redelivery |
| Correct ack | Ack only after process success |
| Ops visibility | Messaging health snapshot + log alerts |
| Delayed redelivery | TTL retry queues → DLX back to work queue (not immediate requeue) |
| Storage growth | Retention sweep for published outbox / old inbox |
| Channel isolation | Optional per-channel queues + workers (ADR-013) |

## References

- Related ADRs: ADR-002 (PostgreSQL), ADR-001 (Microkernel), ADR-013 (channel workers)
- Internal skills: RabbitMQ reliability patterns (at-least-once, DLX, idempotency); Transactional Outbox/Inbox pattern notes
- Design: `OutboxRelayWorker`, `EfOutbox`, `EfInbox`, `RabbitMqNotificationQueue`
