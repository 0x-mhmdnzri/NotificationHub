using System.Security.Claims;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Host.Middleware;

/// <summary>
/// After JwtBearer authentication, populate <see cref="AuthContext"/> so endpoint
/// <c>RequireRoles</c> works for human JWT clients the same way as API keys.
/// API-key AuthContext (set by <see cref="ApiKeyAuthMiddleware"/>) is left untouched.
/// </summary>
public sealed class JwtAuthContextMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Items.ContainsKey(ApiKeyAuthMiddleware.AuthContextItem))
        {
            await next(context);
            return;
        }

        var user = context.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var roles = user.FindAll("role")
                .Concat(user.FindAll(ClaimTypes.Role))
                .Select(c => c.Value)
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var tenant = user.FindFirst("tenant_id")?.Value
                         ?? user.FindFirst("organization_id")?.Value;

            // Map identity roles to legacy AppRoles so HasAnyRole(Admin/Sender/Reader) works
            var mapped = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
            if (roles.Any(r => r is "SuperAdmin" or "PlatformAdmin" or "OrganizationOwner" or "OrganizationAdmin"))
            {
                mapped.Add(AppRoles.Admin);
                mapped.Add(AppRoles.Sender);
                mapped.Add(AppRoles.Reader);
            }
            else if (roles.Any(r => r is "NotificationOperator"))
            {
                mapped.Add(AppRoles.Sender);
                mapped.Add(AppRoles.Reader);
            }
            else if (roles.Any(r => r is "Viewer" or "Auditor"))
            {
                mapped.Add(AppRoles.Reader);
            }

            context.Items[ApiKeyAuthMiddleware.AuthContextItem] = new AuthContext
            {
                ApiKeyId = Guid.Empty,
                TenantId = tenant,
                Roles = mapped.ToArray(),
                KeyName = user.FindFirst("name")?.Value
                          ?? user.FindFirst(ClaimTypes.Email)?.Value
                          ?? user.FindFirst("sub")?.Value
                          ?? "jwt",
                IsJwt = true
            };
        }

        await next(context);
    }
}
