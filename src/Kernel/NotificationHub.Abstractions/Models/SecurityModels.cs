namespace NotificationHub.Abstractions.Models;

public static class AppRoles
{
    public const string Admin = "admin";
    public const string Sender = "sender";
    public const string Reader = "reader";

    public static readonly string[] All = [Admin, Sender, Reader];
}

public sealed record ApiKeyInfo
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? TenantId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public bool IsActive { get; init; } = true;
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
    public DateTimeOffset? LastUsedAt { get; init; }
    /// <summary>Only returned on creation.</summary>
    public string? PlainKey { get; init; }
}

public sealed record CreateApiKeyRequest
{
    public required string Name { get; init; }
    public string? TenantId { get; init; }
    public string[] Roles { get; init; } = [AppRoles.Sender, AppRoles.Reader];
    public DateTimeOffset? ExpiresAt { get; init; }
}

public sealed class AuthContext
{
    public Guid ApiKeyId { get; init; }
    public string? TenantId { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = [];
    public string KeyName { get; init; } = "";
    /// <summary>True when authenticated via JWT (human) rather than API key (machine).</summary>
    public bool IsJwt { get; init; }

    /// <summary>
    /// Admin for API-key role "admin" or platform/org identity roles (SuperAdmin, PlatformAdmin, Organization*).
    /// </summary>
    public bool IsAdmin =>
        Roles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase)
        || Roles.Any(r => string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(r, "PlatformAdmin", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(r, "OrganizationOwner", StringComparison.OrdinalIgnoreCase)
                          || string.Equals(r, "OrganizationAdmin", StringComparison.OrdinalIgnoreCase));

    public bool HasRole(string role) => IsAdmin || Roles.Contains(role, StringComparer.OrdinalIgnoreCase);

    public bool HasAnyRole(params string[] roles)
    {
        if (IsAdmin)
            return true;
        if (roles.Any(HasRole))
            return true;
        // Map identity roles → legacy AppRoles expected by endpoints
        foreach (var r in Roles)
        {
            if (string.Equals(r, "NotificationOperator", StringComparison.OrdinalIgnoreCase)
                && roles.Any(x => string.Equals(x, AppRoles.Sender, StringComparison.OrdinalIgnoreCase)
                               || string.Equals(x, AppRoles.Reader, StringComparison.OrdinalIgnoreCase)))
                return true;
            if (string.Equals(r, "Viewer", StringComparison.OrdinalIgnoreCase)
                && roles.Any(x => string.Equals(x, AppRoles.Reader, StringComparison.OrdinalIgnoreCase)))
                return true;
            if (string.Equals(r, "Auditor", StringComparison.OrdinalIgnoreCase)
                && roles.Any(x => string.Equals(x, AppRoles.Reader, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }
}
