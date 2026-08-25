# Messaging reliability review (NotificationHub)

Date: 2026-08-25  
Skills applied: `rabbitmq`, `transactionalbox`  
Rule: **MassTransit only if multi-message saga becomes primary.**

## Current topology (good baseline)

| Piece | Status |
|--------|--------|
| Durable work queue + DLX/DLQ | ✅ |
| Delayed redelivery via TTL retry queues (no tight nack requeue) | ✅ |
| Publisher confirms | ✅ |
| Manual ack after process | ✅ |
| Prefetch configurable (`PrefetchCount`) | ✅ |
| Inbox dedup by notification id | ✅ |
| Outbox table + relay worker | ✅ (was dual-write vulnerable — fixed) |
| Messaging health + DLQ/outbox lag alerts | ✅ |

Topology (simplified):

```text
API → [DB tx: status + outbox] → OutboxRelay → work exchange → work queue
                                              ↑
                         TTL retry queues ────┘ (delayed redelivery)
work queue fail after MaxRedelivery → DLQ
```

## Issues found and disposition

### 1. Dual-write (Critical) — FIXED
`AcceptAsync` saved status, then a second `SaveChanges` wrote the outbox. Crash window left permanent `Queued` with no publish.

**Fix:** single EF transaction spanning Accept + outbox enqueue on the async send path; `EfOutbox` stages the row so it participates in the ambient transaction.

### 2. Multi-instance relay race (High) — FIXED
Relay selected `pending` without row claim; two instances could publish the same outbox row.

**Fix:** claim batch with `FOR UPDATE SKIP LOCKED`, mark in-flight, then publish.

### 3. Inbox timing (OK by design)
Inbox is marked **after** successful process so delayed redelivery is not short-circuited. Correct for at-least-once + idempotent send.

### 4. In-memory consumer path
Marks inbox *before* process — weaker than Rabbit path. Acceptable only for local/dev.

## TransactionalBox — decision: **do not migrate now**

| Factor | Assessment |
|--------|------------|
| Production readiness | Library disclaimer: not declared production-ready |
| Transport | Official Kafka; RabbitMQ would be custom |
| Overlap | We already have EF outbox + inbox + relay |
| Cost | New dependency + rewrite of enqueue/consume for limited gain |

Keep the custom implementation; improve it (done above) instead of adopting TransactionalBox.

## MassTransit — decision: **not now**

Per project rule: introduce MassTransit **only** when multi-message **sagas** become the primary orchestration model.

Today workflows are in-process (`WorkflowEngine` + DB timeline), not broker sagas. Custom AMQP + outbox is the right fit.

## Remaining hardening (non-blocking)

1. Outbox cleanup job alignment with `Retention:OutboxPublishedDays` (partially covered by retention worker — verify).
2. Poison payload: null deserialize already nacks to DLQ; add metric counter.
3. Channel recovery: connection uses `AutomaticRecoveryEnabled`; re-declare topology on recovery if needed under long outages.
4. Prefer `MessageId` from broker for inbox key when re-publishing delayed copies if business id is reused (currently redelivery reuses same notification id — intentional for idempotency).

## Prefetch & ops
See [prefetch-tuning.md](./prefetch-tuning.md) and [delayed-redelivery.md](./delayed-redelivery.md).
