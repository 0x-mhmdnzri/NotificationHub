# ADR 0010: CQRS with MediatR 12.4.1 — separated read/write pipelines

## Status

Accepted

## Date

2026-08-26

## Context

NotificationHub mixed orchestration, reads, and writes inside minimal API endpoints and a monolithic orchestrator surface. Scaling validation, logging, and future unit-of-work concerns required a uniform application boundary.

## Decision

Adopt **CQRS** via **MediatR 12.4.1**:

- **Commands** (`ICommand` / `ICommand<T>`) — write path (Accept, SendSync, SaveTemplate, …)
- **Queries** (`IQuery<T>`) — read path (GetStatus, GetTemplate, ListTemplates, …)
- **Pipeline behaviors** (ordered):
  1. `ValidationBehavior` — FluentValidation
  2. `LoggingBehavior` — timing + CMD/QRY label
  3. `CommandOnlyBehavior` — write-only hooks (UoW/outbox future)
  4. `QueryOnlyBehavior` — read-only guarantees / future read replicas

Layers:

| Project | Role |
|---------|------|
| `NotificationHub.Application` | Commands, Queries, Handlers, Behaviors, Validators |
| `NotificationHub.Infrastructure` | Composition (`AddInfrastructureCqrs`) |
| `NotificationHub.Core` | Domain services, persistence, messaging (existing) |
| `NotificationHub.Host` | HTTP adapters → `ISender` only |

## Alternatives Considered

### Option A: Keep endpoint-local logic
- **Pros:** Less ceremony
- **Cons:** No uniform validation/logging; hard to test
- **Why rejected:** Does not scale feature surface

### Option B: Full event-sourced CQRS
- **Pros:** Ultimate write/read separation
- **Cons:** Heavy rewrite
- **Why rejected:** Overkill for current product stage

## Consequences

**Positive:**
- Clear write vs read pipelines
- Validators/behaviors cross-cut without endpoint duplication
- Handlers unit-testable with `ISender`

**Negative / trade-offs:**
- More types per feature
- Gradual migration of remaining endpoints

**Risks / follow-up actions:**
- Migrate remaining admin/workflow endpoints to Commands/Queries
- Optional read DbContext / replica for queries

## References

- MediatR 12.4.1
- Skill: dotnet-cqrs-mediatr
