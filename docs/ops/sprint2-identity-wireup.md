# Sprint 2 wire-up (Host Program.cs)

## 1. ApiKeyAuthMiddleware — dual auth pass-through

At start of `InvokeAsync`, after health/swagger checks:

```csharp
if (DualAuthPassThrough.ShouldSkipApiKey(context))
{
    await _next(context);
    return;
}
```

API Key validation for machine clients is unchanged.

## 2. Map endpoints

After other maps:

```csharp
app.MapIdentityAuthEndpoints();
```

Requires JWT (`AddNotificationHubJwtBearer`). When JwtBearer Enabled, also:

```csharp
app.UseAuthentication();
app.UseAuthorization();
```

Place **after** CORS, **before or after** ApiKey middleware as appropriate:
- ApiKey middleware runs first; skips when Bearer or `/api/v1/auth`.
- Then `UseAuthentication` validates JWT for human routes.

## 3. DI

`MembershipService` is picked up by Scrutor (`*Service` → `IMembershipService`) via `AddCorePlatform()`.

## Endpoints

| Method | Path | Auth |
|--------|------|------|
| GET | /api/v1/auth/me | JWT |
| GET | /api/v1/auth/organizations | JWT |
| POST | /api/v1/auth/organizations/switch | JWT |
| POST | /api/v1/auth/logout | JWT |
| POST | /api/v1/auth/invitations | JWT + member.invite |
| POST | /api/v1/auth/invitations/accept | JWT |
