# ADR 0012: Aspire Composition vs Business Orchestration

## Status

Accepted

## Date

2026-08-26

## Context

The platform needs both:

1. **Local/production process topology** — which executables run, which databases and brokers they reference, health probes, and telemetry exporters.
2. **Business broadcast lifecycle** — legal state transitions for a campaign (Draft → … → Completed / Failed / Cancelled) and delivery outcomes per recipient.

A common failure mode in distributed .NET systems is treating **.NET Aspire AppHost** (or Kubernetes manifests) as the “orchestrator” of notification business rules. That conflates *infrastructure composition* with *domain workflow*, making rules hard to test, version, and reason about outside a specific host environment.

We also need OpenTelemetry/Jaeger and Serilog enrichment without scattering one-off setup in every project.

## Decision

### Separation of layers

| Layer | Owner | Responsibility |
|-------|--------|----------------|
| **Application composition** | `NotificationHub.AppHost` | Wire API + channel workers + Postgres + RabbitMQ + Redis + Jaeger; `WaitFor` / references / env |
| **Shared host defaults** | `NotificationHub.ServiceDefaults` | Serilog, OTEL (OTLP), health checks (self + Postgres + Redis + RabbitMQ), service discovery |
| **Business orchestration** | `BroadcastStateMachine`, `CampaignService`, `NotificationOrchestrator` | Domain transitions, accept/process pipeline, completion semantics |
| **Delivery choreography** | Per-channel consumers + plugins | Independent progress per channel; no worker “owns” other channels’ success |

Aspire **must not** encode campaign state rules. Campaign rules **must not** depend on Aspire APIs.

### Observability defaults

- **Serilog**: `Enrich.FromLogContext()`, Application, Environment, MachineName, ThreadId; console text or JSON (`Serilog:UseJsonConsole` / Production).
- **OpenTelemetry**: ASP.NET, HttpClient, EF Core, meters `NotificationHub`; sources `NotificationHub`, `NotificationHub.Broadcast`.
- **Export**: OTLP when `OTEL_EXPORTER_OTLP_ENDPOINT` or `OpenTelemetry:OtlpEndpoint` is set (Jaeger all-in-one in AppHost).
- **Health**: `/health` and `/health/live` (liveness); `/health/ready` includes dependency checks used by Aspire `WithHttpHealthCheck`.

### Host process roles

The same `NotificationHub.Host` binary is composed multiple times under Aspire with `Workers:*` and `RabbitMQ:ConsumeChannel` environment flags (see ADR-013). Defaults remain “all roles on” for monolithic `dotnet run` without AppHost.

## Alternatives considered

### Option A: Encode broadcast steps as Aspire resources / wait chains
- **Pros:** Visible in dashboard as a graph  
- **Cons:** Business rules tied to local tooling; not portable to k8s/CI; untestable as pure domain  
- **Rejected:** Violates domain isolation.

### Option B: MassTransit sagas as the only orchestration
- **Pros:** Mature saga patterns  
- **Cons:** Heavy for current campaign model; ADR-003 deferred MassTransit for core path  
- **Rejected for now:** May revisit for multi-message sagas; campaign lifecycle stays explicit state machine.

### Option C: Ad-hoc logging/tracing per project
- **Pros:** Fast initially  
- **Cons:** Drift, missing enrichers, inconsistent health  
- **Rejected:** Centralized in ServiceDefaults.

## Consequences

**Positive:**
- Clear ownership: infra vs domain vs delivery
- Campaign transitions unit-testable without Aspire
- One place for Serilog/OTEL/health
- AppHost can scale workers independently without changing domain code

**Trade-offs:**
- Operators must learn two “orchestration” words (composition vs domain)
- AppHost and Host role flags must stay documented (this ADR + ADR-013)

**Follow-up:**
- Keep `docs/ops/orchestration-otel-aspire.md` as the runbook aligned to this ADR
- Optional: dedicated worker projects later if Host binary becomes too heavy for channel-only processes

## References

- Related: ADR-003 (RabbitMQ), ADR-011 (campaigns), ADR-013 (channel workers)
- Code: `src/NotificationHub.AppHost`, `src/NotificationHub.ServiceDefaults`, `src/NotificationHub.Core/Orchestration`
