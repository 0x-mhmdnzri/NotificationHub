# Sprint 1 wire-up checklist (Host)

## 1. Make NotificationDbContext partial

In `NotificationDbContext.cs` change:

```csharp
public sealed class NotificationDbContext
```
to:

```csharp
public partial class NotificationDbContext
```

At end of `OnModelCreating`:

```csharp
ConfigureIdentity(modelBuilder);
```

## 2. Program.cs (Host)

After `builder.Services.Configure<OidcOptions>(...)`:

```csharp
builder.Services.AddNotificationHubJwtBearer(builder.Configuration);
```

After existing schema ensures (Phase1Schema, …):

```csharp
await IdentitySchema.EnsureAsync(db, startupLog);
```

`using NotificationHub.Core.Identity;`
`using NotificationHub.Host.Security;`

## 3. appsettings (optional enable)

```json
"Auth": {
  "JwtBearer": {
    "Enabled": false,
    "Authority": "https://localhost:5xxx",
    "Audience": "notificationhub-api",
    "RequireHttpsMetadata": false
  }
}
```

Keep `Enabled: false` until Identity host runs.

## 4. Solution

Add `src/Host/NotificationHub.Identity/NotificationHub.Identity.csproj` to solution under Host folder.

## Stack choice

OpenIddict (free) — see Identity host project.
