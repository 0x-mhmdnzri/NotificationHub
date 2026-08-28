namespace NotificationHub.Application.Abstractions;

/// <summary>Used in unit tests / background workers without HTTP identity.</summary>
public sealed class NullRequestContext : IRequestContext
{
    public static readonly NullRequestContext Instance = new();
    public bool IsAuthenticated => false;
    public string? TenantId => null;
    public IReadOnlyList<string> Roles => Array.Empty<string>();
    public bool IsAdmin => false;
    public bool HasAnyRole(params string[] roles) => false;
}
