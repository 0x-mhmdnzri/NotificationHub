using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Identity;

public sealed class SessionService(NotificationDbContext db, ILogger<SessionService> log) : ISessionService
{
    public async Task<IReadOnlyList<SessionDto>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var rows = await db.Set<UserSessionEntity>().AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.LastSeenAt)
            .Take(50)
            .ToListAsync(ct);

        return rows.Select(s => new SessionDto(
            s.Id,
            s.OrganizationId,
            s.ClientId,
            s.Ip,
            s.UserAgent,
            s.CreatedAt,
            s.LastSeenAt,
            s.ExpiresAt,
            s.IsActive,
            IsCurrent: false)).ToList();
    }

    public async Task<bool> RevokeAsync(Guid userId, Guid sessionId, CancellationToken ct = default)
    {
        var s = await db.Set<UserSessionEntity>().FirstOrDefaultAsync(x => x.Id == sessionId && x.UserId == userId, ct);
        if (s is null)
        {
            return false;
        }

        if (!s.IsActive)
        {
            return true;
        }

        s.IsActive = false;
        s.RevokedAt = DateTimeOffset.UtcNow;
        s.RefreshTokenHash = null;
        await db.SaveChangesAsync(ct);
        log.LogInformation("Session {SessionId} revoked for user {UserId}", sessionId, userId);
        return true;
    }

    public async Task RevokeAllAsync(Guid userId, CancellationToken ct = default)
    {
        var list = await db.Set<UserSessionEntity>().Where(s => s.UserId == userId && s.IsActive).ToListAsync(ct);
        foreach (var s in list)
        {
            s.IsActive = false;
            s.RevokedAt = DateTimeOffset.UtcNow;
            s.RefreshTokenHash = null;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<SessionDto> CreateAsync(CreateSessionRequest request, CancellationToken ct = default)
    {
        var entity = new UserSessionEntity
        {
            UserId = request.UserId,
            OrganizationId = request.OrganizationId,
            ClientId = request.ClientId,
            JwtId = request.JwtId,
            RefreshTokenHash = HashToken(request.RawRefreshToken),
            Ip = Truncate(request.Ip, 64),
            UserAgent = Truncate(request.UserAgent, 256),
            ExpiresAt = DateTimeOffset.UtcNow.Add(request.Lifetime),
            IsActive = true
        };
        db.Set<UserSessionEntity>().Add(entity);
        await db.SaveChangesAsync(ct);
        return new SessionDto(
            entity.Id,
            entity.OrganizationId,
            entity.ClientId,
            entity.Ip,
            entity.UserAgent,
            entity.CreatedAt,
            entity.LastSeenAt,
            entity.ExpiresAt,
            true,
            true);
    }

    public async Task<RefreshResult> RotateRefreshTokenAsync(string rawRefreshToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawRefreshToken))
        {
            return new RefreshResult(false, null, null, null, "invalid_token");
        }

        var hash = HashToken(rawRefreshToken);
        var session = await db.Set<UserSessionEntity>()
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash, ct);

        if (session is null)
        {
            log.LogWarning("Refresh token not found (possible reuse)");
            return new RefreshResult(false, null, null, null, "invalid_token");
        }

        if (!session.IsActive || session.ExpiresAt < DateTimeOffset.UtcNow || session.RevokedAt is not null)
        {
            return new RefreshResult(false, null, null, null, "session_expired");
        }

        var newRaw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        session.RefreshTokenHash = HashToken(newRaw);
        session.LastSeenAt = DateTimeOffset.UtcNow;
        session.JwtId = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync(ct);

        return new RefreshResult(true, session.UserId, session.OrganizationId, newRaw, null);
    }

    static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    static string? Truncate(string? v, int max) =>
        v is null ? null : v.Length <= max ? v : v[..max];
}
