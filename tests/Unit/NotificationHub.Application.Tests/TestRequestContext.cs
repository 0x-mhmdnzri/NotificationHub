using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Tests;

public sealed class TestRequestContext : IRequestContext
{
    public TestRequestContext()
        : this(true, null, AppRoles.Admin, AppRoles.Sender, AppRoles.Reader) { }

    public TestRequestContext(bool authenticated, string? tenantId, params string[] roles)
    {
        IsAuthenticated = authenticated;
        TenantId = tenantId;
        Roles = roles.Length > 0 ? roles : [AppRoles.Admin, AppRoles.Sender, AppRoles.Reader];
    }

    public bool IsAuthenticated { get; }
    public string? TenantId { get; }
    public IReadOnlyList<string> Roles { get; }
    public bool IsAdmin => Roles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase);
    public bool HasAnyRole(params string[] required) =>
        IsAdmin || required.Any(r => Roles.Contains(r, StringComparer.OrdinalIgnoreCase));
}
