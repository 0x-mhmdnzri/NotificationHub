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
3. Start → `Processing` (or `Scheduled`)
4. `CampaignDispatchWorker` claims Pending batches → `NotificationOrchestrator.AcceptAsync` (outbox)
5. Progress derived from durable recipient status counts
6. Idempotency: unique `ContentHash(CampaignId|Channel|Address)`

PostgreSQL tables (nullable columns, unique index). No JSONB for addresses (queryable + unique constraint).

## Consequences

**Positive:** Durable, multi-instance safe, multi-channel cartesian product, CSV streaming.

**Trade-offs:** Worker poll latency; not event-driven claim (acceptable at current scale).

**Follow-up:** UI; COPY bulk insert for very large CSVs; distributed rate limits per provider.
