# ADR-001: Microkernel (Plugin) Architecture

## Status
Accepted

## Context
NotificationHub must support multiple channels (SMS, Email, Push, WhatsApp, ...) and multiple providers per channel. Channels and providers change frequently; the core orchestration must remain stable.

## Decision
Adopt Microkernel Architecture:

- **Core** owns: request validation, preference evaluation, routing/fallback rules, retry policy, delivery status, plugin lifecycle, internal messaging.
- **Plugins** own: actual delivery to a specific provider, channel-specific formatting, provider credentials handling.
- Formal contract in `NotificationHub.Abstractions`.
- Plugins are loaded at startup (currently via DI; later via directory/NuGet discovery + AssemblyLoadContext).

## Consequences
- Core stays thin and highly stable.
- New channels/providers = new plugins, no Core changes.
- Versioning and capability negotiation become important.
- Slightly higher initial complexity than a monolithic service.

## Governance
- Semantic Versioning for Abstractions and Core.
- Plugins declare capabilities.
- Future: automated validation pipeline and compatibility matrix.
