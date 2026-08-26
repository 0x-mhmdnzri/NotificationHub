# ADR 0010: CQRS with MediatR 12.4.1 — Vertical Slices & separated data-flow pipelines

## Status

Accepted (supersedes initial thin MediatR wiring)

## Date

2026-08-26

## Context

Initial CQRS wiring used horizontal folders and service-backed queries. The updated engineering standard requires:

- Vertical slice feature folders
- Result pattern for business failures (not exceptions)
- Query path: `AsNoTracking` + projection (no aggregate load)
- Explicit command vs query intent markers
- Pipeline order: Logging → Validation → Authorization → Performance → Handler
- No command chaining; thin HTTP adapters via `ISender`
- Trusted tenant context (not client-only)

## Decision

### Structure (Vertical Slice)

```text
Features/
  Notifications/
    Accept/     (command)
    SendSync/   (command)
    GetStatus/  (query + DTO projection)
  Templates/
    Save/       (command)
    GetByKey/   (query + DTO)
    List/       (query + DTO)
```

### Markers

- `ICommand` / `ICommand<T>` — write pipeline
- `IQuery<T>` — read pipeline (side-effect free for business state)

### Result

`Result` / `Result<T>` + `Error` / `ErrorType` mapped to HTTP only in Host (`ResultHttpExtensions`).

### Query rules

Handlers inject `NotificationDbContext` and use:

- `AsNoTracking()`
- `Select` projections to DTOs
- tenant filter from **trusted** API context

### Command rules

Handlers orchestrate existing domain services (`NotificationOrchestrator`, template engine).  
Outbox dual-write remains inside Core accept path (already implemented).

### Pipeline order

1. LoggingBehavior  
2. ValidationBehavior (FluentValidation)  
3. AuthorizationBehavior  
4. PerformanceBehavior (slow threshold 500ms)  
5. Handler  

MediatR **12.4.1** only. Assembly: `ApplicationAssemblyMarker`.

## Consequences

**Positive:** Clear read/write data flows, testable handlers, transport-independent errors, navigable features.

**Trade-offs:** Gradual migration of remaining endpoints; Application references EF for query composition (Level 1–2 CQRS, same DB).

**Completed migrations:** Notifications, Templates, Preferences, Webhooks, Consents, Workflows, Segments, Engagement, Devices, Topics, Messaging health.

**Follow-up:** Real AuthorizationBehavior policies; optional TransactionBehavior for multi-write; optional read replica if measured need.
