# ADR 0015: Scrutor Convention-Based DI

## Status
Accepted

## Context
Program.cs accumulated dozens of manual `AddScoped`/`AddSingleton` lines for homogeneous services, repositories, and plugins.

## Decision
Use **Scrutor 4.2.2** with **narrow assembly filters** (skill-aligned):

| Module | Method | What is scanned |
|--------|--------|-----------------|
| Application | `AddApplication` | MediatR remains authority for handlers; FluentValidation assembly scan |
| Infrastructure | `AddInfrastructureCqrs` | Domain ports (`I*Repository`, `IUnitOfWork`, `IDomainEventDispatcher`) |
| Infrastructure | `AddHangfireJobs` | `*Job` classes in HangfireJobs namespace |
| Infrastructure | `AddIntegrationMessaging` | Integration bridge types |
| Core | `AddCorePlatform` | `*Service` → matching interface; workflow handlers; security; templates; providers |
| Host | `AddChannelPlugins` | `IPlugin` from `NotificationHub.Plugins.*` assemblies |

### Explicit (not Scrutor)
- Redis vs InMemory rate limiter / inbox bus
- Hangfire vs Null outbox scheduler
- RabbitMQ publisher factory
- Preference caching decorator
- Conditional hosted workers
- Options configuration

### Non-goals
- Scanning Domain entities
- Replacing MediatR handler registration
- `FromApplicationDependencies()` / unfiltered `AddClasses()`

## Consequences
+ Less boilerplate in Program.cs
+ Predictable conventions per module
- New service must follow naming/`AssignableTo` convention or be registered explicitly
