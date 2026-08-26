namespace NotificationHub.Application.Abstractions;

/// <summary>
/// Trusted request identity — populated by the host from auth middleware, never from client body alone.
/// </summary>
public interface IRequestContext
{
    bool IsAuthenticated { get; }
    string? TenantId { get; }
    IReadOnlyList<string> Roles { get; }
    bool IsAdmin { get; }
    bool HasAnyRole(params string[] roles);
}
