using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NotificationHub.Core.Identity;

namespace NotificationHub.Host.Http;

public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/auth").RequireAuthorization();

        g.MapGet("/sessions", async (ClaimsPrincipal user, ISessionService sessions, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(sub, out var userId))
                return Results.Unauthorized();
            var list = await sessions.ListAsync(userId, ct);
            return Results.Ok(list);
        });

        g.MapDelete("/sessions/{sessionId:guid}", async (
            Guid sessionId, ClaimsPrincipal user, ISessionService sessions, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(sub, out var userId))
                return Results.Unauthorized();
            var ok = await sessions.RevokeAsync(userId, sessionId, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        });

        g.MapDelete("/sessions", async (ClaimsPrincipal user, ISessionService sessions, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(sub, out var userId))
                return Results.Unauthorized();
            await sessions.RevokeAllAsync(userId, ct);
            return Results.NoContent();
        });

        return app;
    }
}
