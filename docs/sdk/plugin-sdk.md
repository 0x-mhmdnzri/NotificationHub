# NotificationHub Plugin SDK (F21)

## Contract
Implement `IChannelPlugin` (extends `IPlugin`):

```csharp
public interface IChannelPlugin : IPlugin
{
    string Channel { get; }
    Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken ct = default);
}
```

Lifecycle: `InitializeAsync` → `StartAsync` → `SendAsync` / `HealthCheckAsync` → `StopAsync`.

## Rules
1. No secrets in source — read from `IPluginContext.Configuration`.
2. HTTP timeout ≤ 20s; block loopback webhooks.
3. Return `DeliveryResult` with stable `ErrorCode` on failure.
4. `Id` must be unique and stable across versions (`{channel}-{provider}`).
5. Ship as `net9.0` class library referencing only `NotificationHub.Abstractions`.

## Certification
Run `ChannelPluginCertificationTests` patterns: identity, safety helpers, unconfigured send.

## Load
- Compile-time: register `AddSingleton<IPlugin, YourPlugin>()` in Host.
- Runtime: drop DLL under `Plugins:Directory` and `POST /api/v1/admin/plugins/reload`.
