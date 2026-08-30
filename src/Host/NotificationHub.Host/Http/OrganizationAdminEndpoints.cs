using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using NotificationHub.Core.Identity;

namespace NotificationHub.Host.Http;

public static class OrganizationAdminEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var orgs = app.MapGroup("/api/v1/organizations").WithTags("Organizations");

        orgs.MapPost("/", async (
            CreateOrgRequest body,
            HttpContext http,
            IOrganizationAdminService orgsSvc,
            IMembershipService memberships,
            IAuthorizationService authz,
            CancellationToken ct) =>
        {
            if (!TryUser(http, out var userId))
                return Results.Unauthorized();

            // Platform-only create for now (or first-org bootstrap later)
            if (!http.User.IsInRole(IdentityRoles.PlatformAdmin) &&
                !http.User.Claims.Any(c => c.Type == "role" && c.Value == IdentityRoles.PlatformAdmin))
                return Results.Json(new { error = "platform_admin_required" }, statusCode: 403);

            var dto = await orgsSvc.CreateAsync(body.Name, body.Slug, body.Type ?? "Merchant", ct);
            await memberships.RecordSecurityEventAsync("OrganizationCreated", userId, dto.Id, dto.Name, ct);
            return Results.Created($"/api/v1/organizations/{dto.Id}", dto);
        }).WithName("CreateOrganization");

        orgs.MapGet("/{id:guid}", async (
            Guid id,
            HttpContext http,
            IOrganizationAdminService orgsSvc,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!TryUser(http, out var userId))
                return Results.Unauthorized();

            var snap = await memberships.GetActiveMembershipAsync(userId, id, ct);
            if (snap is null && !IsPlatform(http))
                return Results.Json(new { error = "forbidden" }, statusCode: 403);

            if (snap is not null &&
                !snap.Permissions.Contains(IdentityPermissions.OrganizationRead, StringComparer.OrdinalIgnoreCase) &&
                !IsPlatform(http))
                return Results.Json(new { error = "forbidden" }, statusCode: 403);

            var dto = await orgsSvc.GetAsync(id, ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        }).WithName("GetOrganization");

        orgs.MapPatch("/{id:guid}", async (
            Guid id,
            UpdateOrgRequest body,
            HttpContext http,
            IOrganizationAdminService orgsSvc,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!TryUser(http, out var userId))
                return Results.Unauthorized();

            if (!await RequirePerm(memberships, userId, id, IdentityPermissions.OrganizationUpdate, http, ct))
                return Results.Json(new { error = "forbidden" }, statusCode: 403);

            var dto = await orgsSvc.UpdateAsync(id, body.Name, body.Status, ct);
            if (dto is null) return Results.NotFound();
            await memberships.RecordSecurityEventAsync("OrganizationUpdated", userId, id, body.Status ?? body.Name, ct);
            return Results.Ok(dto);
        }).WithName("UpdateOrganization");

        orgs.MapGet("/{id:guid}/members", async (
            Guid id,
            HttpContext http,
            IOrganizationAdminService orgsSvc,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!TryUser(http, out var userId))
                return Results.Unauthorized();

            if (!await RequirePerm(memberships, userId, id, IdentityPermissions.MemberRead, http, ct))
                return Results.Json(new { error = "forbidden" }, statusCode: 403);

            return Results.Ok(await orgsSvc.ListMembersAsync(id, ct));
        }).WithName("ListOrganizationMembers");

        orgs.MapPost("/{id:guid}/members/{membershipId:guid}/roles", async (
            Guid id,
            Guid membershipId,
            RoleNameRequest body,
            HttpContext http,
            IOrganizationAdminService orgsSvc,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!TryUser(http, out var userId))
                return Results.Unauthorized();

            if (!await RequirePerm(memberships, userId, id, IdentityPermissions.MemberRoleAssign, http, ct))
                return Results.Json(new { error = "forbidden" }, statusCode: 403);

            // Block assigning PlatformAdmin via org API
            if (string.Equals(body.RoleName, IdentityRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase))
                return Results.Json(new { error = "cannot_assign_platform_role" }, statusCode: 403);

            var ok = await orgsSvc.AssignRoleAsync(membershipId, body.RoleName, ct);
            if (!ok) return Results.BadRequest(new { error = "assign_failed" });
            await memberships.RecordSecurityEventAsync("RoleChanged", userId, id, $"+{body.RoleName}", ct);
            return Results.NoContent();
        }).WithName("AssignMemberRole");

        orgs.MapDelete("/{id:guid}/members/{membershipId:guid}/roles/{roleName}", async (
            Guid id,
            Guid membershipId,
            string roleName,
            HttpContext http,
            IOrganizationAdminService orgsSvc,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!TryUser(http, out var userId))
                return Results.Unauthorized();

            if (!await RequirePerm(memberships, userId, id, IdentityPermissions.MemberRoleAssign, http, ct))
                return Results.Json(new { error = "forbidden" }, statusCode: 403);

            var ok = await orgsSvc.RemoveRoleAsync(membershipId, roleName, ct);
            if (!ok) return Results.BadRequest(new { error = "remove_failed" });
            await memberships.RecordSecurityEventAsync("RoleChanged", userId, id, $"-{roleName}", ct);
            return Results.NoContent();
        }).WithName("RemoveMemberRole");

        orgs.MapPost("/{id:guid}/members/{membershipId:guid}/status", async (
            Guid id,
            Guid membershipId,
            MembershipStatusRequest body,
            HttpContext http,
            IOrganizationAdminService orgsSvc,
            IMembershipService memberships,
            CancellationToken ct) =>
        {
            if (!TryUser(http, out var userId))
                return Results.Unauthorized();

            if (!await RequirePerm(memberships, userId, id, IdentityPermissions.MemberSuspend, http, ct))
                return Results.Json(new { error = "forbidden" }, statusCode: 403);

            var ok = await orgsSvc.SetMembershipStatusAsync(membershipId, body.Status, ct);
            if (!ok) return Results.BadRequest(new { error = "status_failed" });
            await memberships.RecordSecurityEventAsync("UserSuspended", userId, id, body.Status, ct);
            return Results.NoContent();
        }).WithName("SetMembershipStatus");

        return app;
    }

    static bool TryUser(HttpContext http, out Guid userId)
    {
        userId = default;
        if (http.User?.Identity?.IsAuthenticated != true) return false;
        var sub = http.User.FindFirstValue("sub") ?? http.User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(sub, out userId);
    }

    static bool IsPlatform(HttpContext http) =>
        http.User.Claims.Any(c =>
            (c.Type is "role" or ClaimTypes.Role) &&
            c.Value.Equals(IdentityRoles.PlatformAdmin, StringComparison.OrdinalIgnoreCase));

    static async Task<bool> RequirePerm(
        IMembershipService memberships, Guid userId, Guid orgId, string permission, HttpContext http, CancellationToken ct)
    {
        if (IsPlatform(http)) return true;
        var snap = await memberships.GetActiveMembershipAsync(userId, orgId, ct);
        return snap is not null &&
               snap.Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);
    }

    public sealed record CreateOrgRequest(string Name, string? Slug, string? Type);
    public sealed record UpdateOrgRequest(string? Name, string? Status);
    public sealed record RoleNameRequest(string RoleName);
    public sealed record MembershipStatusRequest(string Status);
}
