# ADR 0005: Domain-Driven Design Transformation

## Status
Accepted (incremental)

## Context
NotificationHub grew as an anemic model: DTOs in Abstractions, god services in Core, EF entities as the source of truth, and status mutations scattered across services. This violates DDD principles from Evans (invariants in domain, behavior over setters, small consistency boundaries).

## Decision
Introduce a pure `NotificationHub.Domain` layer and migrate core subdomains incrementally.

### Bounded contexts (strategic)
| Context | Core concepts | Notes |
|---------|---------------|-------|
| **Delivery** | Notification, DeliveryStatus, Channel, TemplateKey | Single-delivery consistency |
| **Broadcast** | BroadcastCampaign, CampaignStatus | Lifecycle only; recipients outside aggregate |
| **Preferences** | UserPreference, Consent | (next increment) |
| **Templates** | NotificationTemplate | (next increment) |
| **Identity** | ApiKey | Supporting subdomain |
| **Channels** | Plugins / providers | Generic subdomain |

### Aggregates (tactical) — phase 1
1. **Notification** — status transitions, attempts, suppress/collapse/cancel
2. **BroadcastCampaign** — schedule/start/cancel/complete; **recipients are NOT inside the aggregate** (fan-out size)

### Dependency rule
```
Host → Application → Domain
         ↓
   Infrastructure → Domain (implements ports)
```
Domain has **zero** package references to EF, RabbitMQ, ASP.NET.

### Outbox / messaging
Domain raises `IDomainEvent`. Application/Infrastructure maps to integration events + Outbox. Aggregates never reference RabbitMQ.

## Consequences
- Positive: invariants testable without DB; ubiquitous language in code; illegal transitions fail in domain
- Cost: dual path during migration (legacy Core services + new Domain); mapping layer until EF maps to domain entities
- Risk: big-bang rewrite rejected; migrate use case by use case

## Migration order
1. Domain project + Notification + BroadcastCampaign + tests ✅
2. Repository adapters + AcceptNotificationHandler domain path
3. Replace CampaignService mutations with BroadcastCampaign aggregate
4. Preferences / Templates aggregates
5. Architecture tests enforcing Domain ↛ Infrastructure

## Increment 2 (completed)

- EF repositories: Notification, BroadcastCampaign, UserPreference, NotificationTemplate
- CampaignService Create/Start/Cancel/Complete route through `BroadcastCampaign` aggregate + `BroadcastCampaignMapper`
- `IDomainEventDispatcher` → `OutboxDomainEventDispatcher` (durable integration events)
- Architecture tests: Domain ↛ Infrastructure / EF / RabbitMQ
- Aggregates: UserPreference, NotificationTemplate
