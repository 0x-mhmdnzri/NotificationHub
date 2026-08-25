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

    public bool IsAdmin => Roles.Contains(AppRoles.Admin, StringComparer.OrdinalIgnoreCase);
    public bool HasRole(string role) => IsAdmin || Roles.Contains(role, StringComparer.OrdinalIgnoreCase);
    public bool HasAnyRole(params string[] roles) => IsAdmin || roles.Any(HasRole);
}
