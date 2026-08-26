namespace NotificationHub.Application.Abstractions;

/// <summary>
/// Declarative role requirement for a command/query. Evaluated by AuthorizationBehavior.
/// Empty Roles = any authenticated principal. Admin always passes.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class AuthorizeRolesAttribute : Attribute
{
    public AuthorizeRolesAttribute(params string[] roles) => Roles = roles;
    public string[] Roles { get; }
    /// <summary>When true, unauthenticated requests are rejected (default).</summary>
    public bool RequireAuthenticated { get; init; } = true;
}
