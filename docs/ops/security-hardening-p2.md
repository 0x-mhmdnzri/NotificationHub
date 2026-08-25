# Security hardening P2 (2026-08-25)

## SEC-14 Admin IP allowlist
Config (optional):
```json
"Auth": {
  "AdminIpAllowlist": [ "203.0.113.10", "198.51.100.0" ]
}
```
or env: `Auth__AdminIpAllowlist__0=203.0.113.10`  
Empty list = disabled (role checks still apply). Applies to paths under `/api/v1/admin`.

Behind a reverse proxy, configure `ForwardedHeaders` so `RemoteIpAddress` is the real client IP.

## SEC-15 Error sanitization
`ExceptionHandlingMiddleware` catches unhandled exceptions:
- Production: `{ error, correlationId }` only
- Development: includes `detail` + exception type
Full exception is always logged server-side with CorrelationId.

## SEC-16 CORS
```json
"Cors": {
  "AllowedOrigins": [ "https://app.example.com" ]
}
```
Empty = no CORS policy registered (API clients / same-origin only).  
When set: methods GET/POST/PUT/DELETE, headers Content-Type + X-Api-Key + X-Correlation-ID.

## SEC-17 NuGet audit
CI job `nuget-audit` runs `dotnet list package --vulnerable` and fails on High/Critical.

## SEC-18 Dockerfile
- Multi-stage publish
- Non-root `appuser` (nologin)
- `DOTNET_EnableDiagnostics=0`
- No SDK in final image

## SEC-19 Correlation ID
- Request header `X-Correlation-ID` accepted or generated
- Echoed on response
- Added to logging scope (CorrelationId, RequestPath)
- Never log API keys, secrets, or full connection strings
