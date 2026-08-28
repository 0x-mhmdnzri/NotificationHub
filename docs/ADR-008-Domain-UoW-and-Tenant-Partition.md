# ADR 0008: Domain Accept Transaction + Optional Tenant Partitioning

## Status
Accepted

## Context
EF adapters for Domain aggregates existed, but:

1. Production Accept still built anemic `NotificationStatus` without Domain lifecycle
2. `SaveAsync` committed status before outbox → dual-write risk under failure
3. Delivery worker updated status with raw enums, bypassing Aggregate invariants
4. Ordering per tenant was not available under competing consumers

## Decision

### A. Domain-first Accept + single transaction
```text
Notification.Accept(...)  // domain invariants
  → stage status (no SaveChanges)
  → stage delivery outbox row
  → BeginTransaction + SaveChanges + Commit
```

### B. Delivery worker uses Aggregate for status transitions
`MarkProcessing` / `MarkSent` / `MarkFailed` via `INotificationRepository` when available.

### C. Optional partition-by-tenant
`RabbitMQ:PartitionByTenant` + `TenantPartitionCount`:

```text
partition = hash(TenantId) % N
queue/routing key suffix .t{partition}
```

**When to enable:** multi-tenant SaaS needing **per-tenant ordering** with horizontal parallelism across tenants.

**When to keep false (default):** single-tenant or ordering not required — simpler topology.

## Consequences
- Accept is transactional with outbox (ADR dual-write fix completed for staged Save)
- Domain invariants enforced on process path when repo is registered
- Tenant partitions increase queue count (N × channels); operational cost

## Related
- ADR-005 DDD, ADR-006 Workers, ADR-007 Latency
