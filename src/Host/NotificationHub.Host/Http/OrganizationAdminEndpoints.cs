using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NotificationHub.Core.Identity;

namespace NotificationHub.Host.Http;

public static class OrganizationAdminEndpoints
{
    public static IEndpointRouteBuilder MapOrganizationAdminEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/organizations").RequireAuthorization();

        g.MapGet("/{organizationId:guid}", async (Guid organizationId, IOrganizationAdminService svc, CancellationToken ct) =>
        {
            var o = await svc.GetAsync(organizationId, ct);
            return o is null ? Results.NotFound() : Results.Ok(o);
        }).RequireAuthorization(IdentityPermissions.OrganizationRead);

        g.MapPost("/", async (CreateOrgBody body, IOrganizationAdminService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Name))
                return Results.BadRequest(new { error = "name_required" });
            var o = await svc.CreateAsync(body.Name, body.Slug, body.Type ?? "Merchant", ct);
            return Results.Created($"/api/v1/organizations/{o.Id}", o);
        }).RequireAuthorization(IdentityPermissions.OrganizationCreate);

        g.MapPatch("/{organizationId:guid}", async (Guid organizationId, UpdateOrgBody body, IOrganizationAdminService svc, CancellationToken ct) =>
        {
            var o = await svc.UpdateAsync(organizationId, body.Name, body.Status, ct);
            return o is null ? Results.NotFound() : Results.Ok(o);
        }).RequireAuthorization(IdentityPermissions.OrganizationUpdate);

        g.MapGet("/{organizationId:guid}/members", async (Guid organizationId, IOrganizationAdminService svc, CancellationToken ct) =>
        {
            var list = await svc.ListMembersAsync(organizationId, ct);
            return Results.Ok(list);
        }).RequireAuthorization(IdentityPermissions.MemberRead);

        g.MapPost("/{organizationId:guid}/members/{membershipId:guid}/roles", async (
            Guid organizationId, Guid membershipId, RoleBody body, IOrganizationAdminService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.RoleName))
                return Results.BadRequest(new { error = "role_required" });
            var ok = await svc.AssignRoleAsync(membershipId, body.RoleName, ct);
            return ok ? Results.NoContent() : Results.BadRequest(new { error = "assign_failed" });
        }).RequireAuthorization(IdentityPermissions.MemberRoleAssign);

        g.MapDelete("/{organizationId:guid}/members/{membershipId:guid}/roles/{roleName}", async (
            Guid organizationId, Guid membershipId, string roleName, IOrganizationAdminService svc, CancellationToken ct) =>
        {
            var ok = await svc.RemoveRoleAsync(membershipId, roleName, ct);
            return ok ? Results.NoContent() : Results.NotFound();
        }).RequireAuthorization(IdentityPermissions.MemberRoleAssign);

        g.MapPatch("/{organizationId:guid}/members/{membershipId:guid}/status", async (
            Guid organizationId, Guid membershipId, StatusBody body, IOrganizationAdminService svc, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(body.Status))
                return Results.BadRequest(new { error = "status_required" });
            var ok = await svc.SetMembershipStatusAsync(membershipId, body.Status, ct);
            return ok ? Results.NoContent() : Results.BadRequest(new { error = "status_failed" });
        }).RequireAuthorization(IdentityPermissions.MemberSuspend);

        g.MapPost("/{organizationId:guid}/invitations", async (
            Guid organizationId, InviteBody body, ClaimsPrincipal user, IMembershipService memberships, CancellationToken ct) =>
        {
            var sub = user.FindFirstValue("sub") ?? user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(sub, out var actorId))
                return Results.Unauthorized();
            if (string.IsNullOrWhiteSpace(body.Email))
                return Results.BadRequest(new { error = "email_required" });
            var result = await memberships.InviteAsync(organizationId, body.Email, body.RoleName, actorId, ct);
            return result.Success
                ? Results.Ok(new { invitationId = result.InvitationId })
                : Results.BadRequest(new { error = result.Error });
        }).RequireAuthorization(IdentityPermissions.MemberInvite);

        return app;
    }

    public record CreateOrgBody(string Name, string? Slug, string? Type);
    public record UpdateOrgBody(string? Name, string? Status);
    public record RoleBody(string RoleName);
    public record StatusBody(string Status);
    public record InviteBody(string Email, string? RoleName);
}
