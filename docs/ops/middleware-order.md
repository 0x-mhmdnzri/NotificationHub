# Middleware order (Program.cs)

Aligned with ASP.NET Core guidance (AuthN before AuthZ, Exception early, CORS before Auth):

1. `UseForwardedHeaders`
2. `ExceptionHandlingMiddleware` (Validation + Authorization + unhandled)
3. `CorrelationIdMiddleware`
4. `UseHsts` + `UseHttpsRedirection` (non-Development)
5. `SecurityHeadersMiddleware`
6. `UseResponseCompression`
7. Swagger (Development)
8. `UseRouting`
9. `UseCors` (when origins configured) — **before** auth for preflight
10. `AdminIpAllowlistMiddleware`
11. `ApiKeyAuthMiddleware` (AuthN)
12. Endpoint `RequireRoles` + MediatR `[AuthorizeRoles]` (AuthZ)
13. `Map*` endpoints

Do **not** place CORS after API-key auth, or preflight OPTIONS will fail without a key.
