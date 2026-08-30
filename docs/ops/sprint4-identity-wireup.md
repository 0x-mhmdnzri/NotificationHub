# Sprint 4 wire-up

## Program.cs (API Host)

```csharp
app.UseMiddleware<AuthRateLimitMiddleware>(); // after correlation, before/after ApiKey as preferred
app.MapIdentityAuthEndpoints();
app.MapSessionEndpoints();
app.MapOrganizationAdminEndpoints();
```

## DI

`SessionService` registered via Scrutor (`*Service` → interface).

## Identity host

- Configure `Identity:Signing:RsaPrivateKeyPem` + `KeyId` in production.
- Expose JWKS for API `AddNotificationHubJwtBearer` Authority validation.
- On token issue: call `ISessionService.CreateAsync` with raw refresh (hash only stored).
- On refresh: `RotateRefreshTokenAsync` — single-use rotation, fail-closed on reuse.

## Rate limits

`RateLimiting:AuthSensitivePerMinute` (default 20) for invite / switch / logout / sessions.

## OpenAPI

See `docs/openapi/identity-v1-freeze.md` — Admin UI must generate client from this contract.
