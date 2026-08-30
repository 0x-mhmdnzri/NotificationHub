using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace NotificationHub.Core.Identity;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

/// <summary>Deny-by-default. SuperAdmin succeeds all. PlatformAdmin succeeds all in-tenant.</summary>
public sealed class PermissionAuthorizationHandler(
    IMembershipService memberships) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
            return;

        if (context.User.IsInRole(IdentityRoles.SuperAdmin)
            || context.User.Claims.Any(c =>
                (c.Type is "role" or ClaimTypes.Role)
                && string.Equals(c.Value, IdentityRoles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            context.Succeed(requirement);
            return;
        }

        var sub = context.User.FindFirst("sub")?.Value
                  ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var tenant = context.User.FindFirst("tenant_id")?.Value
                     ?? context.User.FindFirst("organization_id")?.Value;
        if (!Guid.TryParse(sub, out var userId))
            return;

        if (tenant is null || !Guid.TryParse(tenant, out var orgId))
        {
            var platform = await memberships.GetPlatformRolesAsync(userId);
            if (platform.Contains(IdentityRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase)
                || platform.Contains(IdentityRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase))
            {
                context.Succeed(requirement);
            }
            return;
        }

        var snap = await memberships.GetActiveMembershipAsync(userId, orgId);
        if (snap is null)
            return;

        if (snap.Roles.Contains(IdentityRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase)
            || snap.Roles.Contains(IdentityRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase))
        {
            context.Succeed(requirement);
            return;
        }

        if (snap.Permissions.Contains(requirement.Permission, StringComparer.OrdinalIgnoreCase))
            context.Succeed(requirement);
    }
}

public sealed class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    readonly DefaultAuthorizationPolicyProvider _fallback;

    public PermissionPolicyProvider(Microsoft.Extensions.Options.IOptions<AuthorizationOptions> options)
    {
        _fallback = new DefaultAuthorizationPolicyProvider(options);
    }

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.Contains('.', StringComparison.Ordinal))
        {
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new PermissionRequirement(policyName))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();
}
