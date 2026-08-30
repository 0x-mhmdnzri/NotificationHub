# Identity partial

`NotificationDbContext` must be marked `partial` and call `ConfigureIdentity(modelBuilder)` at the end of `OnModelCreating`.

If the main file is not yet partial, apply:

```csharp
public partial class NotificationDbContext : DbContext
```

and inside OnModelCreating:

```csharp
ConfigureIdentity(modelBuilder);
```
