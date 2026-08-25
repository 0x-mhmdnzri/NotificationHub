# ADR 0004: Regional SMS Providers (Kavenegar and Sms.ir)

## Status

Accepted

## Date

2026-08-25

## Context

NotificationHub must send SMS in Iran. Global SMS providers are not a practical default due to availability, payment, numbering, and operational constraints.

Business and technical forces:

- Primary SMS traffic is domestic.
- Provider reliability and cost vary; failover between local providers is desirable.
- The architecture already supports multiple plugins per channel.
- Maintaining unused global provider integrations increases config surface and operational confusion.

Twilio was initially introduced as a generic SMS option, then removed because it is not part of the real operating environment.

## Decision

We will support **Kavenegar** and **Sms.ir** as the SMS providers for NotificationHub.

- Preferred SMS provider defaults to `sms-kavenegar`.
- Fallback order defaults to `["sms-kavenegar", "sms-smsir"]`.
- Twilio is not included in the codebase, solution, Docker build, or configuration templates.
- Provider selection remains config-driven through the `Providers` section.

## Alternatives Considered

### Option A: Keep Twilio as an optional plugin
- **Pros:**
  - Useful for international expansion later
  - Familiar global baseline
- **Cons:**
  - Dead configuration and dependency weight in the current market
  - Increases onboarding noise for a team that cannot use it operationally
- **Why rejected:**
  - Current product scope is Iran-first; unused provider code is active liability.

### Option B: Single SMS provider only (Kavenegar)
- **Pros:**
  - Simplest setup
- **Cons:**
  - No failover when one local provider degrades
- **Why rejected:**
  - Dual-provider fallback is a low-cost reliability improvement under the plugin model.

### Option C: Aggregate through a commercial omnichannel SaaS
- **Pros:**
  - One vendor integration
- **Cons:**
  - Less direct control, potential cost and residency constraints
- **Why rejected:**
  - Direct local provider control is preferred.

## Consequences

**Positive:**
- Codebase matches real deployment constraints
- Clear SMS ownership and fallback policy
- Smaller dependency and configuration surface

**Negative / trade-offs:**
- International SMS is not covered out of the box
- Local provider API quirks remain our integration responsibility

**Risks / follow-up actions:**
- Add provider-level health metrics and automatic temporary deprioritization
- Document number formatting and sender-line constraints per provider
- Revisit global providers only when there is a concrete non-Iran traffic requirement

## References

- Related ADRs:
  - ADR 0001: Microkernel architecture
- Design docs:
  - `Plugins/NotificationHub.Plugins.Sms.Kavenegar/`
  - `Plugins/NotificationHub.Plugins.Sms.SmsIr/`
  - `Providers` section in appsettings
- Discussion threads / tickets:
  - Removal of Twilio and Iran-first provider decision (2026-08-25)
