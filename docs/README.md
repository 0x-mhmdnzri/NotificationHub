# NotificationHub documentation

## Architecture Decision Records (ADR)

ADRs capture **why** a decision was made, alternatives considered, and consequences. They are the source of truth for architectural intent; code may lag slightly, but docs should not contradict Accepted ADRs.

| ID | File | Status | Summary |
|----|------|--------|---------|
| 001 | [ADR-001-Microkernel-Architecture.md](ADR-001-Microkernel-Architecture.md) | Accepted | Core + plugins; channels as packages |
| 002 | [ADR-002-PostgreSQL-PgBouncer-Persistence.md](ADR-002-PostgreSQL-PgBouncer-Persistence.md) | Accepted | PostgreSQL primary store; optional PgBouncer |
| 003 | [ADR-003-RabbitMQ-Queue.md](ADR-003-RabbitMQ-Queue.md) | Accepted | Outbox + RabbitMQ + inbox; channel routing |
| 004 | [ADR-004-Regional-SMS-Providers.md](ADR-004-Regional-SMS-Providers.md) | Accepted | Regional SMS (Kavenegar, Sms.ir, Twilio) |
| 010 | [ADR-010-CQRS-MediatR.md](ADR-010-CQRS-MediatR.md) | Accepted | MediatR commands/queries, vertical slices |
| 011 | [ADR-011-Batch-Broadcast-Campaigns.md](ADR-011-Batch-Broadcast-Campaigns.md) | Accepted | Campaign + recipients + dispatch worker |
| 012 | [ADR-012-Aspire-Composition-vs-Business-Orchestration.md](ADR-012-Aspire-Composition-vs-Business-Orchestration.md) | Accepted | Aspire ≠ business orchestration |
| 013 | [ADR-013-Per-Channel-Delivery-Workers.md](ADR-013-Per-Channel-Delivery-Workers.md) | Accepted | Separate email/sms/push consumers |
| 019 | [ADR-019-Multi-Tenant-Identity-RBAC-ABAC.md](ADR-019-Multi-Tenant-Identity-RBAC-ABAC.md) | Proposed | B2B org identity, OIDC, RBAC/ABAC (API Key unchanged) |

### Writing a new ADR

1. Copy the structure from an existing Accepted ADR (Status, Date, Context, Decision, Alternatives, Consequences, References).
2. Use the next free number (`ADR-0NN-...`).
3. Link it from this index and from the root [README](../README.md).
4. Prefer amending an existing ADR with a dated **Amendment** section when changing an Accepted decision, rather than rewriting history silently.

## Backlogs

- [Multi-tenant identity (backend-first)](backlog/multi-tenant-identity.md)

## Operations

Practical runbooks under [`ops/`](ops/):

- [commit-conventions.md](ops/commit-conventions.md) — Conventional Commits (`feat`, `fix`, `ci`, …)
- [versioning.md](ops/versioning.md) — SemVer 2.0.0, tags `vX.Y.Z`, release workflow, `GET /api/v1/version`
- [orchestration-otel-aspire.md](ops/orchestration-otel-aspire.md) — Aspire topology, Serilog, Jaeger, health
- [messaging-reliability.md](ops/messaging-reliability.md) — outbox, ack, DLQ, delayed redelivery
- [prefetch-tuning.md](ops/prefetch-tuning.md), [latency.md](ops/latency.md)
- Security hardening notes (`security-hardening-*.md`)

Contributor overview: [CONTRIBUTING.md](../CONTRIBUTING.md).

## SDK

- [plugin-sdk.md](sdk/plugin-sdk.md) — implementing channel plugins
