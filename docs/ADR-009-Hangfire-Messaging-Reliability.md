# ADR 0009: Hangfire for Messaging Reliability (Outbox Dispatch)

## Status
Accepted

## Context
NotificationHub already uses transactional outbox + RabbitMQ + inbox. OutboxRelayWorker polled every few hundred ms. Skill `messaging-reliability-hangfire` requires Hangfire as a **durable job engine**, not a broker replacement.

## Decision

```text
HTTP Accept
   → DB TX: status + OutboxMessage
   → COMMIT
   → Hangfire.Enqueue(DispatchAsync(outboxId))   // ID only
        → Load outbox
        → RabbitMQ.Publish
        → Mark published
   → Consumer: Inbox + at-least-once delivery
```

### Components
| Piece | Role |
|-------|------|
| Hangfire + PostgreSQL storage | Durable job execution / retry |
| `OutboxDispatchJob` | Publish by outbox id; AutomaticRetry(5) |
| `HangfireOutboxDispatchScheduler` | Enqueue after COMMIT |
| `OutboxReconciliationJob` | Recurring scan of stuck pending |
| `OutboxRelayWorker` | Optional safety net (`KeepRelayWorker`) |
| RabbitMQ | Messaging backbone |

### Semantics
- **At-least-once** job execution
- Publish may duplicate if crash after RabbitMQ success before mark published → consumer **inbox** must stay idempotent
- Hangfire is **not** exactly-once

### Why not Hangfire-only without RabbitMQ?
Delivery fan-out, channel routing, prefetch, DLX, competing consumers remain RabbitMQ strengths.

## Configuration
```json
"HangfireMessaging": {
  "Enabled": true,
  "KeepRelayWorker": true,
  "ReconciliationIntervalMinutes": 2,
  "StuckPendingSeconds": 30
}
```
Dashboard: `/hangfire` (lock down auth in production).

## Consequences
+ Lower latency than pure polling under load (push job after commit)
+ Durable retries independent of process poll loop
+ Reconciliation recovers missed enqueues
- Extra Hangfire schema tables in PostgreSQL
- Must protect dashboard
