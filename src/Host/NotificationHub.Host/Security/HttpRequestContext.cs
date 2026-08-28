using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Host.Middleware;

namespace NotificationHub.Host.Security;

/// <summary>Bridges ApiKeyAuthMiddleware AuthContext into Application IRequestContext.</summary>
public sealed class HttpRequestContext(IHttpContextAccessor accessor) : IRequestContext
{
    private AuthContext? Auth =>
        accessor.HttpContext?.Items[ApiKeyAuthMiddleware.AuthContextItem] as AuthContext;

    public bool IsAuthenticated => Auth is not null;
    public string? TenantId => Auth?.TenantId;
    public IReadOnlyList<string> Roles => Auth?.Roles ?? Array.Empty<string>();
    public bool IsAdmin => Auth?.IsAdmin ?? false;

    public bool HasAnyRole(params string[] roles) =>
        Auth?.HasAnyRole(roles) ?? false;
}
