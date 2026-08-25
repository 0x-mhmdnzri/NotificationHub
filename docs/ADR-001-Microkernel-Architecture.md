# ADR 0001: Microkernel (Plugin) Architecture for NotificationHub

## Status

Accepted

## Date

2026-08-25

## Context

NotificationHub is a multi-channel notification service that must deliver messages over email, SMS, and later additional channels (push, WhatsApp, in-app, chat). Each channel can have multiple third-party providers, and those providers change frequently due to regional availability, pricing, deliverability, and regulatory constraints.

Key forces:

- **Extensibility:** New channels and providers must be added without rewriting core orchestration.
- **Stability:** Core behaviors (idempotency, retry, status tracking, preferences, routing) must remain stable while delivery integrations evolve.
- **Regional constraints:** In Iran, SMS providers such as Kavenegar and Sms.ir are the practical options; global providers like Twilio are not usable. Provider set is market-specific.
- **Team velocity:** Delivery integrations are high-churn. Tight coupling between orchestration and provider SDKs creates long-lived merge conflict and regression risk.
- **Operational isolation:** A failure in one provider SDK should not cascade into core API availability.

The system also needs a formal plugin contract so that optional capabilities (HTML email, delivery reports, attachments, regional providers) can be declared and negotiated.

Alternatives relevant to this context include a monolithic service with hard-coded provider clients, a pure strategy-pattern library without lifecycle/hosting, and adopting an existing notification platform as a black box.

## Decision

We will implement NotificationHub using a **Microkernel (Plugin) Architecture**.

- The **Core (microkernel)** owns:
  - request acceptance and validation
  - idempotency
  - template rendering orchestration
  - preference evaluation
  - provider selection and fallback order
  - retry policy
  - delivery status tracking
  - audit logging
  - webhook dispatch triggers
  - plugin lifecycle management
- **Plugins** own:
  - provider-specific authentication and SDK usage
  - last-mile send calls
  - channel/provider capability declaration
- The formal contract lives in `NotificationHub.Abstractions` (`IPlugin`, `IChannelPlugin`, capability descriptors, lifecycle hooks).
- Host (`NotificationHub.Host`) composes Core + plugins and exposes the HTTP API.

This option was chosen because it keeps the high-churn provider surface area outside the stable kernel, while still allowing one unified send pipeline.

## Alternatives Considered

### Option A: Monolithic service with direct provider SDKs in Core
- **Pros:**
  - Fastest initial implementation
  - Fewer projects and less indirection
- **Cons:**
  - Core becomes coupled to every SDK
  - Adding/removing a provider requires core changes and redeploy of the whole system
  - Harder to isolate provider failures and version conflicts
- **Why rejected:**
  - Provider churn and regional constraints make core stability the primary requirement.

### Option B: Strategy pattern only (no plugin lifecycle / host model)
- **Pros:**
  - Simple and idiomatic in .NET
  - Easy DI registration
- **Cons:**
  - No formal capability model, versioning, or lifecycle hooks
  - Weak story for dynamic loading, sandboxing, and third-party extension later
- **Why rejected:**
  - Insufficient for the intended extensibility model and long-term plugin governance.

### Option C: Buy/use an external notification platform as the system of record
- **Pros:**
  - Faster time-to-market for generic multi-channel features
  - Less operational ownership of queue/status infrastructure
- **Cons:**
  - Limited control over regional SMS providers and data residency
  - Vendor lock-in and cost model risk
  - Harder to enforce custom preference, audit, and routing rules
- **Why rejected:**
  - Product needs first-class control over Iranian SMS providers and internal orchestration policies.

## Consequences

**Positive:**
- Core remains minimal and relatively stable.
- New providers (for example Sms.ir) are additive plugins, not core rewrites.
- Capability declaration enables routing/fallback decisions without hard-coding provider names throughout business logic.
- Clear ownership boundary between orchestration and delivery.

**Negative / trade-offs:**
- Higher initial structural complexity (multiple projects, contracts, host composition).
- Plugin versioning and compatibility must be governed intentionally.
- Debugging spans Core + plugin boundaries.

**Risks / follow-up actions:**
- Enforce semantic versioning on `NotificationHub.Abstractions`.
- Keep provider preference/fallback configuration externalized (`Providers` section in appsettings).
- Evolve from DI-only registration toward directory/NuGet discovery when third-party plugins become real.
- Add automated plugin validation (contract compliance, health checks) in CI.

## References

- Related ADRs:
  - ADR 0002: PostgreSQL + PgBouncer for status persistence
  - ADR 0003: RabbitMQ as the notification queue
  - ADR 0004: Regional SMS providers (Kavenegar, Sms.ir)
- Design docs:
  - `README.md`
  - Solution structure under `src/` and `Plugins/`
- Discussion threads / tickets:
  - Initial architecture decision during Phase 1 implementation (2026-08-25)
