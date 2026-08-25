# Security hardening P0 (2026-08-25)

Implements SEC-01 … SEC-06 from issue #1.

## SEC-01 Bootstrap API key
- Removed hard-coded `dev-secret-key-change-me` from committed appsettings.
- Production refuses known-weak keys and requires `Auth__BootstrapApiKey` (or `Auth:BootstrapApiKey`) when no keys exist in DB.
- Development may auto-generate a one-time key and log it once.

## SEC-02 Auth rate limit
- `RateLimiting:AuthFailuresPerMinute` (default 30) applied per client IP in `ApiKeyAuthMiddleware` before/around key validation.

## SEC-03 Access control
- `RequireRoles` on templates, preferences, audit, workflows, segments, analytics, compliance export, in-app, admin monitoring/providers.
- `GET /api/v1/notifications/{id}` requires Reader/Sender/Admin and enforces tenant match via `CanAccessTenant`.

## SEC-04 Webhook SSRF
- `WebhookUrlValidator`: HTTPS only, no loopback, block private/link-local/CGNAT/metadata ranges, resolve DNS and re-check.
- Applied on register and on dispatch.
- Named HttpClient `webhooks` timeout 10s.

## SEC-05 Security headers
- `SecurityHeadersMiddleware`: nosniff, frame deny, referrer no-referrer, Permissions-Policy, strict CSP for API, HSTS on HTTPS.

## SEC-06 Config hygiene
- `AllowedHosts` no longer `*` in base config.
- Production appsettings leaves secrets empty (env/secret store).
- `.env.example` documents required vars without real secrets.

## Local first-run
```bash
export Auth__BootstrapApiKey="$(openssl rand -hex 32)"
# or set AUTH_BOOTSTRAP_API_KEY and map in compose
dotnet run --project src/NotificationHub.Host
```
