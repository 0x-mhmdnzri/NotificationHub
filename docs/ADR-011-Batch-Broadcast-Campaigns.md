# ADR 0011: Batch Broadcast Campaigns

## Status

Accepted

## Date

2026-08-26

## Context

Need production batch advertising: ingest recipients (manual list or CSV), select channels, track progress, scale via workers, remain consistent with existing Outbox/RabbitMQ pipeline.

## Decision

Introduce **Campaign** + **BroadcastRecipient** durable model:

1. Create campaign (template + channels[])
2. Add recipients (list or streaming CSV)
3. Start → lifecycle enters active states (`Preparing` / `Processing` / …)
4. `CampaignDispatchWorker` claims Pending batches → `NotificationOrchestrator.AcceptAsync` (outbox)
5. Progress derived from durable recipient status counts
6. Idempotency: unique content hash (`CampaignId|Channel|Address`)

PostgreSQL tables (nullable columns, unique index). No JSONB for addresses (queryable + unique constraint).

### Lifecycle (state machine)

Explicit transitions live in `BroadcastStateMachine` (business orchestration — **not** Aspire):

- Guards via `CanTransition` / `EnsureTransition` / `Transition` (OTEL activity on transition)
- Terminal resolution via `ResolveCompletion` (Completed / PartiallyCompleted / Failed / still Delivering)
- Recipient delivery status remains a separate enum from campaign lifecycle

See **ADR-012** for composition vs domain orchestration boundaries.

## Consequences

**Positive:** Durable, multi-instance safe, multi-channel cartesian product, CSV streaming; testable lifecycle rules.

**Trade-offs:** Worker poll latency; not event-driven claim (acceptable at current scale).

**Follow-up:** UI; COPY bulk insert for very large CSVs; distributed rate limits per provider; channel workers scale delivery after dispatch (ADR-013).

## References

- Related: ADR-003, ADR-012, ADR-013
- Code: `CampaignService`, `CampaignDispatchWorker`, `BroadcastStateMachine`
