# ADR 0012: Solution & Folder Structure (Microkernel)

## Status
Accepted

## Context
Flat `src/*` + flat `Plugins/*` made boundaries unclear for a senior-maintained microkernel system.

## Decision

Physical layout mirrors architectural roles (assembly names unchanged):

```text
src/
  Host/              Composition root (API, Aspire, ServiceDefaults)
  Kernel/            Extension contracts (Abstractions / IPlugin)
  BuildingBlocks/    Domain, Application, Infrastructure, Core (platform)
  Clients/           Public SDK
Plugins/
  Email|Sms|Chat|Push|InApp/   Channel plugins by capability
tests/
  Architecture/ Unit/ Benchmarks/
```

Solution folders (Visual Studio / Rider):

```text
01 - Host
02 - Kernel
03 - BuildingBlocks
04 - Clients
05 - Plugins/{Email,Sms,Chat,Push,InApp}
06 - Tests/{Architecture,Unit,Benchmarks}
07 - Tools
```

## Dependency direction (enforced)

```text
Host → BuildingBlocks + Kernel + Plugins
Kernel.Abstractions ← Plugins, Application, Core
Domain ↛ Infrastructure
```

## Non-goals (this ADR)
- Renaming assemblies (would churn NuGet/SDK clients)
- Splitting every channel plugin into Domain/Application/Infrastructure projects (premature until a plugin needs independent lifecycle)

## Related
ADR-005 DDD, Microkernel skill
