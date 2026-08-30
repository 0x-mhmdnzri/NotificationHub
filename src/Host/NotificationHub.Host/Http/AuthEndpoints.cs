using System.Security.Claims;
using NotificationHub.Core.Identity;

namespace NotificationHub.Host.Http;

/// <summary>Human identity endpoints (JWT). API Key path unchanged for machine APIs.</summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapIdentityAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/auth").WithTags("Auth");

        g.MapGet("/me", async (HttpContext http, IMembershipService memberships, CancellationToken ct) =>
        {
            if (!TryGetUserId(http, out var userId))
                return Results.Unauthorized();

            var orgId = TryGetOrgId(http);
            var me = await memberships.GetMeAsync(userId, orgId, ct);
            if (me is null)
                return Results.Unauthorized();

            return Results.Ok(new
            {
                user = new { id = me.UserId, email = me.Email, displayName = me.DisplayName },
                tenant = me.OrganizationId is null ? null : new { id = me.OrganizationId, name = me.OrganizationName },
                membershipId = me.MembershipId,
                roles = me.Roles,
                permissions = me.Permissions
            });
        }).WithName("AuthMe");

        g.MapGet("/organizations", async (HttpContext http, IMembershipService memberships, CancellationToken ct) =>
        {
            if (!TryGetUserId(http, out var userId))
                return Results.Unauthorized();

            var list = await memberships.ListMembershipsAsync(userId, ct);
            return Results.Ok(list.Select(m => new
            {
                membershipId = m.MembershipId,
                organizationId = m.OrganizationId,
                name = m.OrganizationName,
                organizationStatus = m.OrganizationStatus,
                membershipStatus = m.MembershipStatus,
                roles = m.Roles
            }));
        }).WithName("AuthListOrganizations");

        g.MapPost("/organizations/switch", async (
            SwitchOrgRequest body,
            HttpContext http,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!TryGetUserId(http, out var userId))
                return Results.Unauthorized();

            var snap = await memberships.GetActiveMembershipAsync(userId, body.OrganizationId, ct);
            if (snap is null)
                return Results.Json(new { error = "membership_inactive_or_missing" }, statusCode: StatusCodes.Status403Forbidden);

            await memberships.RecordSecurityEventAsync("TenantMembershipChanged", userId, body.OrganizationId, "switch", ct);

            // Token re-issue is Identity host responsibility; API confirms membership + returns context for BFF/client.
            return Results.Ok(new
            {
                organizationId = snap.OrganizationId,
                membershipId = snap.MembershipId,
                roles = snap.Roles,
                permissions = snap.Permissions,
                note = "Client must request new access token with tenant_id from Identity host"
            });
        }).WithName("AuthSwitchOrganization");

        g.MapPost("/logout", async (HttpContext http, IMembershipService memberships, CancellationToken ct) =>
        {
            if (!TryGetUserId(http, out var userId))
                return Results.Unauthorized();

            var jti = http.User.FindFirst("jti")?.Value;
            await memberships.RevokeSessionAsync(userId, null, jti, ct);
            await memberships.RecordSecurityEventAsync("Logout", userId, TryGetOrgId(http), null, ct);
            return Results.NoContent();
        }).WithName("AuthLogout");

        g.MapPost("/invitations", async (
            CreateInviteRequest body,
            HttpContext http,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!TryGetUserId(http, out var userId))
                return Results.Unauthorized();

            var orgId = body.OrganizationId ?? TryGetOrgId(http);
            if (orgId is null)
                return Results.BadRequest(new { error = "organization_required" });

            var snap = await memberships.GetActiveMembershipAsync(userId, orgId.Value, ct);
            if (snap is null || !snap.Permissions.Contains(IdentityPermissions.MemberInvite, StringComparer.OrdinalIgnoreCase))
                return Results.Json(new { error = "forbidden" }, statusCode: StatusCodes.Status403Forbidden);

            var result = await memberships.InviteAsync(orgId.Value, body.Email, body.RoleName, userId, ct);
            if (!result.Success)
                return Results.BadRequest(new { error = result.Error });

            return Results.Created($"/api/v1/auth/invitations/{result.InvitationId}", new { id = result.InvitationId });
        }).WithName("AuthCreateInvitation");

        g.MapPost("/invitations/accept", async (
            AcceptInviteRequest body,
            HttpContext http,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!TryGetUserId(http, out var userId))
                return Results.Unauthorized();

            var ok = await memberships.AcceptInviteAsync(body.Token, userId, ct);
            return ok ? Results.NoContent() : Results.BadRequest(new { error = "invalid_or_expired_invitation" });
        }).WithName("AuthAcceptInvitation");

        return app;
    }

    static bool TryGetUserId(HttpContext http, out Guid userId)
    {
        userId = default;
        if (http.User?.Identity?.IsAuthenticated != true)
            return false;
        var sub = http.User.FindFirstValue("sub") ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    static Guid? TryGetOrgId(HttpContext http)
    {
        var v = http.User.FindFirstValue("tenant_id") ?? http.User.FindFirstValue("organization_id");
        return Guid.TryParse(v, out var g) ? g : null;
    }

    public sealed record SwitchOrgRequest(Guid OrganizationId);
    public sealed record CreateInviteRequest(string Email, string? RoleName, Guid? OrganizationId);
    public sealed record AcceptInviteRequest(string Token);
}
