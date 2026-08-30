using System.Security.Claims;
using NotificationHub.Core.Identity;

namespace NotificationHub.Host.Security;

/// <summary>Builds ITenantContext / ISecurityContext from JWT principal (human path only).</summary>
public sealed class JwtTenantContext(IHttpContextAccessor accessor) : ITenantContext
{
    ClaimsPrincipal? User => accessor.HttpContext?.User;

    public Guid? OrganizationId => ParseGuid(User?.FindFirst("tenant_id")?.Value
        ?? User?.FindFirst("organization_id")?.Value);

    public Guid? UserId => ParseGuid(User?.FindFirst("sub")?.Value
        ?? User?.FindFirst(ClaimTypes.NameIdentifier)?.Value);

    public Guid? MembershipId => ParseGuid(User?.FindFirst("membership_id")?.Value);

    static Guid? ParseGuid(string? v) => Guid.TryParse(v, out var g) ? g : null;
}

public sealed class JwtSecurityContextFactory(IHttpContextAccessor accessor)
{
    public ISecurityContext Create()
    {
        var user = accessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated != true)
            return new NullSecurityContext();

        var roles = user.FindAll("role").Select(c => c.Value)
            .Concat(user.FindAll(ClaimTypes.Role).Select(c => c.Value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Permissions resolved server-side in later sprints; roles only in token for now.
        var perms = user.FindAll("permission").Select(c => c.Value).ToArray();

        return new SecurityContext
        {
            UserId = Guid.TryParse(user.FindFirst("sub")?.Value, out var uid) ? uid : null,
            OrganizationId = Guid.TryParse(user.FindFirst("tenant_id")?.Value, out var tid) ? tid : null,
            MembershipId = Guid.TryParse(user.FindFirst("membership_id")?.Value, out var mid) ? mid : null,
            Roles = roles,
            Permissions = perms,
            IsAuthenticated = true,
            IsPlatformUser = roles.Contains(IdentityRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase),
            IsMfaSatisfied = user.FindAll("amr").Any(c => c.Value is "mfa" or "otp" or "pwd")
        };
    }
}
