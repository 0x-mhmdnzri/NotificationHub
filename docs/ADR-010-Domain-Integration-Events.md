# ADR 0010: Domain Events vs Integration Events + Domain Suppress Path

## Status
Accepted

## Context
Domain events previously leaked into outbox as serialized domain types (VOs, assembly-coupled).
Preference/consent suppress path built anemic `NotificationStatus` without Aggregate transitions.

## Decision

### 1. Two event layers
| Layer | Location | Consumers |
|-------|----------|-----------|
| **Domain Event** | `NotificationHub.Domain` | In-process only (handlers, outbox staging) |
| **Integration Event** | `NotificationHub.Abstractions.IntegrationEvents` | Other services, webhooks, analytics |

Mapping: `DomainEventToIntegrationMapper` flattens VOs → primitives, assigns stable `eventType` + `version`.

Outbox payload shape for integration:
```json
{ "kind": "integration", "eventType": "notification.suppressed", "version": 1, "payload": { ... } }
```

Delivery outbox remains `NotificationRequest` JSON (no `kind` field).

### 2. Suppress via Aggregate
```text
Notification.Accept(...)  // Queued/Scheduled + NotificationAccepted
  → Suppress(reason)      // → Suppressed + NotificationSuppressed
  → persist status + integration outbox (no delivery publish)
```

Preference deny and consent deny both use this path.

### 3. What is NOT published
`NotificationMarkedProcessing` stays internal (no integration mapping).

## Consequences
+ Consumers independent of Domain assembly
+ Versioned contracts (`NotificationAcceptedV1`, …)
+ Suppress invariants enforced by Aggregate
- Mapping maintenance when new domain events appear
