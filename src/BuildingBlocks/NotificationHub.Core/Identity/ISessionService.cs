namespace NotificationHub.Core.Identity;

public interface ISessionService
{
    Task<IReadOnlyList<SessionDto>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid userId, Guid sessionId, CancellationToken ct = default);
    Task RevokeAllAsync(Guid userId, CancellationToken ct = default);
    Task<SessionDto> CreateAsync(CreateSessionRequest request, CancellationToken ct = default);
    Task<RefreshResult> RotateRefreshTokenAsync(string rawRefreshToken, CancellationToken ct = default);
}

public sealed record SessionDto(
    Guid Id,
    Guid? OrganizationId,
    string? ClientId,
    string? Ip,
    string? UserAgent,
    DateTimeOffset CreatedAt,
    DateTimeOffset LastSeenAt,
    DateTimeOffset ExpiresAt,
    bool IsActive,
    bool IsCurrent);

public sealed record CreateSessionRequest(
    Guid UserId,
    Guid? OrganizationId,
    string? ClientId,
    string JwtId,
    string RawRefreshToken,
    string? Ip,
    string? UserAgent,
    TimeSpan Lifetime);

public sealed record RefreshResult(
    bool Success,
    Guid? UserId,
    Guid? OrganizationId,
    string? NewRawRefreshToken,
    string? Error);
