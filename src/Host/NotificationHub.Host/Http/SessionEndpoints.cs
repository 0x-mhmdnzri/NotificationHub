using System.Security.Claims;
using NotificationHub.Core.Identity;

namespace NotificationHub.Host.Http;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/auth/sessions").WithTags("Auth");

        g.MapGet("/", async (HttpContext http, ISessionService sessions, CancellationToken ct) =>
        {
            if (!TryUser(http, out var userId))
                return Results.Unauthorized();

            var currentJti = http.User.FindFirstValue("jti");
            var list = await sessions.ListAsync(userId, ct);
            // Mark current by matching stored JwtId if available — client uses IsActive + timestamps.
            return Results.Ok(list.Select(s => new
            {
                id = s.Id,
                organizationId = s.OrganizationId,
                clientId = s.ClientId,
                ip = s.Ip,
                userAgent = s.UserAgent,
                createdAt = s.CreatedAt,
                lastSeenAt = s.LastSeenAt,
                expiresAt = s.ExpiresAt,
                isActive = s.IsActive
            }));
        }).WithName("ListSessions");

        g.MapDelete("/{sessionId:guid}", async (
            Guid sessionId,
            HttpContext http,
            ISessionService sessions,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!TryUser(http, out var userId))
                return Results.Unauthorized();

            var ok = await sessions.RevokeAsync(userId, sessionId, ct);
            if (!ok) return Results.NotFound();
            await memberships.RecordSecurityEventAsync("SessionRevoked", userId, null, sessionId.ToString(), ct);
            return Results.NoContent();
        }).WithName("RevokeSession");

        g.MapPost("/revoke-all", async (
            HttpContext http,
            ISessionService sessions,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!TryUser(http, out var userId))
                return Results.Unauthorized();

            await sessions.RevokeAllAsync(userId, ct);
            await memberships.RecordSecurityEventAsync("SessionRevoked", userId, null, "all", ct);
            return Results.NoContent();
        }).WithName("RevokeAllSessions");

        return app;
    }

    static bool TryUser(HttpContext http, out Guid userId)
    {
        userId = default;
        if (http.User?.Identity?.IsAuthenticated != true) return false;
        var sub = http.User.FindFirstValue("sub") ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }
}
