# ADR 0013: Per-Channel Delivery Workers

## Status

Accepted

## Date

2026-08-26

## Context

A single shared `notifications` queue and one consumer process couples email, SMS, and push delivery:

- A slow or failing SMS provider can starve email throughput under shared prefetch.
- Scaling is all-or-nothing (scale the whole consumer, not one channel).
- Operational isolation (deploy, restart, rate limits) is harder.

Aspire makes it natural to run **multiple processes** of the same Host with different configuration. RabbitMQ already supports distinct queues and routing keys.

Constraints:

- Keep transactional outbox on the API path (ADR-003)
- Avoid three full copies of the codebase
- Preserve monolithic single-process mode for simple local/dev runs

## Decision

### 1. Channel routing on the broker (amendment to ADR-003 topology)

When `RabbitMQ:ChannelRouting` is `true` (default under Aspire):

- Work queues: `notifications.{channel}` (e.g. `notifications.email`)
- Routing keys: `notification.send.{channel}`
- Per-queue TTL retry queues and shared DLX/DLQ pattern as in ADR-003
- Publish path resolves channel from `NotificationRequest.Channel` or `Channels[0]` (fallback `email`)

When `ChannelRouting` is `false`, legacy single queue `notifications` remains available.

### 2. Process roles via configuration

Same binary: `NotificationHub.Host`.

| Process (Aspire name) | Key env |
|-----------------------|---------|
| `notification-api` | `Workers:RunDeliveryConsumer=false`, `Workers:RunOutboxRelay=true` |
| `worker-email` | `RabbitMQ:ConsumeChannel=email`, delivery consumer on, outbox/campaign/etc off |
| `worker-sms` | `ConsumeChannel=sms`, … |
| `worker-push` | `ConsumeChannel=push`, … |

`NotificationBackgroundWorker` consumes only the queue selected by `RabbitMQ:ConsumeChannel` (or the legacy queue if unset and not channel-routed).

### 3. Choreography, not cross-channel orchestration

Each worker processes messages for its channel independently. Campaign completion semantics remain in the **business** layer (`BroadcastStateMachine.ResolveCompletion`), not in worker coordination RPCs.

## Alternatives considered

### Option A: Single queue + competing consumers only
- **Pros:** Simplest topology  
- **Cons:** No channel isolation or independent scale  
- **Rejected** for Aspire/production scale path; still valid for tiny monoliths with `ChannelRouting=false`.

### Option B: Separate Worker projects per channel
- **Pros:** Smaller deploy artifacts, fewer plugin references  
- **Cons:** More solution surface, DI duplication  
- **Deferred:** Role flags on Host are enough for current phase (see ADR-012 follow-up).

### Option C: Kafka topic-per-channel
- **Pros:** Strong partitioning story  
- **Cons:** Stack already standardized on RabbitMQ (ADR-003)  
- **Rejected.**

## Consequences

**Positive:**
- Independent scale and failure domains per channel
- Clear Aspire dashboard process graph
- Outbox remains single writer on API; workers stay pure consumers

**Trade-offs:**
- More processes and queue objects to monitor
- Operators must set `ConsumeChannel` correctly or a worker idles on the wrong queue
- Multi-channel sends become multiple messages (one per channel) at accept/dispatch time — already consistent with campaign cartesian product (ADR-011)

**Follow-up:**
- Alert per-queue depth (`notifications.email`, …) and DLQ
- Optional chat/inapp dedicated workers using the same pattern
- Document prefetch per channel in `docs/ops/prefetch-tuning.md` when SLOs differ

## References

- Related: ADR-003, ADR-011, ADR-012
- Code: `RabbitMqNotificationQueue`, `NotificationBackgroundWorker`, `NotificationHub.AppHost/Program.cs`, Host `Workers:*` registration
