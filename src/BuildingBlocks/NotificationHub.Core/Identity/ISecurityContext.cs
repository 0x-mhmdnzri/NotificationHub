namespace NotificationHub.Core.Identity;

/// <summary>Immutable, server-derived security snapshot for the current request.</summary>
public interface ISecurityContext
{
    Guid? UserId { get; }
    Guid? OrganizationId { get; }
    Guid? MembershipId { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    bool IsAuthenticated { get; }
    bool IsPlatformUser { get; }
    bool IsMfaSatisfied { get; }
    bool HasPermission(string permission);
    bool HasAnyRole(params string[] roles);
}

public sealed class NullSecurityContext : ISecurityContext
{
    public Guid? UserId => null;
    public Guid? OrganizationId => null;
    public Guid? MembershipId => null;
    public IReadOnlyCollection<string> Roles => Array.Empty<string>();
    public IReadOnlyCollection<string> Permissions => Array.Empty<string>();
    public bool IsAuthenticated => false;
    public bool IsPlatformUser => false;
    public bool IsMfaSatisfied => false;
    public bool HasPermission(string permission) => false;
    public bool HasAnyRole(params string[] roles) => false;
}

public sealed class SecurityContext : ISecurityContext
{
    public required Guid? UserId { get; init; }
    public required Guid? OrganizationId { get; init; }
    public required Guid? MembershipId { get; init; }
    public required IReadOnlyCollection<string> Roles { get; init; }
    public required IReadOnlyCollection<string> Permissions { get; init; }
    public required bool IsAuthenticated { get; init; }
    public required bool IsPlatformUser { get; init; }
    public bool IsMfaSatisfied { get; init; }

    public bool HasPermission(string permission) =>
        Permissions.Contains(permission, StringComparer.OrdinalIgnoreCase);

    public bool HasAnyRole(params string[] roles) =>
        roles.Any(r => Roles.Contains(r, StringComparer.OrdinalIgnoreCase));
}
