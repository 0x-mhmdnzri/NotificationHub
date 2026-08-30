using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Devices;

public sealed class DeviceService : IDeviceService
{
    private static readonly HashSet<string> Platforms = new(StringComparer.OrdinalIgnoreCase)
    { "apns", "fcm", "webpush", "expo" };

    private readonly NotificationDbContext _db;
    public DeviceService(NotificationDbContext db) => _db = db;

    public async Task<DeviceRegistration> RegisterAsync(RegisterDeviceRequest request, CancellationToken ct = default)
    {
        if (!Platforms.Contains(request.Platform))
            throw new ArgumentException($"Platform must be one of: {string.Join(", ", Platforms)}");
        if (string.IsNullOrWhiteSpace(request.Token) || request.Token.Length > 512)
            throw new ArgumentException("Token required and max 512 chars");

        var existing = await _db.DeviceTokens.FirstOrDefaultAsync(x =>
            x.UserId == request.UserId && x.Token == request.Token && x.Platform == request.Platform.ToLowerInvariant(), ct);

        if (existing is null)
        {
            existing = new DeviceTokenEntity
            {
                UserId = request.UserId,
                TenantId = request.TenantId,
                Platform = request.Platform.ToLowerInvariant(),
                Token = request.Token,
                Locale = request.Locale,
                IsActive = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            _db.DeviceTokens.Add(existing);
        }
        else
        {
            existing.IsActive = true;
            existing.TenantId = request.TenantId ?? existing.TenantId;
            existing.Locale = request.Locale ?? existing.Locale;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return ToModel(existing);
    }

    public async Task<bool> UnregisterAsync(string userId, string token, string? tenantId, CancellationToken ct = default)
    {
        var e = await _db.DeviceTokens.FirstOrDefaultAsync(x => x.UserId == userId && x.Token == token, ct);
        if (e is null)
            return false;
        if (!string.IsNullOrEmpty(tenantId) && e.TenantId != tenantId)
            return false;
        e.IsActive = false;
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<DeviceRegistration>> ListAsync(string userId, string? tenantId, CancellationToken ct = default)
    {
        var q = _db.DeviceTokens.AsNoTracking().Where(x => x.UserId == userId && x.IsActive);
        if (!string.IsNullOrEmpty(tenantId))
            q = q.Where(x => x.TenantId == tenantId);
        return (await q.ToListAsync(ct)).Select(ToModel).ToList();
    }

    private static DeviceRegistration ToModel(DeviceTokenEntity e) => new()
    {
        Id = e.Id,
        UserId = e.UserId,
        TenantId = e.TenantId,
        Platform = e.Platform,
        Token = e.Token,
        Locale = e.Locale,
        IsActive = e.IsActive,
        CreatedAt = e.CreatedAt,
        UpdatedAt = e.UpdatedAt
    };
}
