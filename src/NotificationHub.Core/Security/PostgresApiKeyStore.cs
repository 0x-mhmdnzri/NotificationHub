using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Security;

public sealed class PostgresApiKeyStore : IApiKeyStore
{
    private readonly NotificationDbContext _db;
    public PostgresApiKeyStore(NotificationDbContext db) => _db = db;

    public async Task<ApiKeyInfo> CreateAsync(CreateApiKeyRequest request, string plainKey, string keyHash, CancellationToken ct = default)
    {
        var roles = NormalizeRoles(request.Roles);
        var id = ApiKeyHasher.TryParseKeyId(plainKey, out var parsed) ? parsed : Guid.NewGuid();
        var entity = new ApiKeyEntity
        {
            Id = id,
            Name = request.Name,
            KeyHash = keyHash,
            TenantId = request.TenantId,
            RolesJson = JsonSerializer.Serialize(roles),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = request.ExpiresAt
        };
        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync(ct);
        return ToInfo(entity, plainKey);
    }

    public async Task<ApiKeyRecord?> FindByHashAsync(string keyHash, CancellationToken ct = default)
    {
        var e = await _db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(x => x.KeyHash == keyHash && x.IsActive, ct);
        if (e is null) return null;
        if (e.ExpiresAt.HasValue && e.ExpiresAt < DateTimeOffset.UtcNow) return null;
        return ToRecord(e);
    }

    public async Task<ApiKeyRecord?> FindByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (e is null) return null;
        if (e.ExpiresAt.HasValue && e.ExpiresAt < DateTimeOffset.UtcNow) return null;
        return ToRecord(e);
    }

    public async Task TouchLastUsedAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.ApiKeys.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return;
        e.LastUsedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ApiKeyInfo>> ListAsync(string? tenantId = null, CancellationToken ct = default)
    {
        var q = _db.ApiKeys.AsNoTracking().Where(x => x.IsActive);
        if (tenantId is not null) q = q.Where(x => x.TenantId == tenantId);
        var list = await q.OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        return list.Select(e => ToInfo(e, null)).ToList();
    }

    public async Task<bool> RevokeAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _db.ApiKeys.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (e is null) return false;
        e.IsActive = false;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static ApiKeyRecord ToRecord(ApiKeyEntity e) => new()
    {
        Id = e.Id,
        Name = e.Name,
        KeyHash = e.KeyHash,
        TenantId = e.TenantId,
        Roles = JsonSerializer.Deserialize<string[]>(e.RolesJson) ?? [],
        IsActive = e.IsActive,
        ExpiresAt = e.ExpiresAt
    };

    private static string[] NormalizeRoles(string[]? roles)
    {
        var set = (roles ?? []).Select(r => r.Trim().ToLowerInvariant()).Where(r => AppRoles.All.Contains(r)).Distinct().ToArray();
        return set.Length == 0 ? [AppRoles.Reader] : set;
    }

    private static ApiKeyInfo ToInfo(ApiKeyEntity e, string? plain)
        => new()
        {
            Id = e.Id, Name = e.Name, TenantId = e.TenantId,
            Roles = JsonSerializer.Deserialize<string[]>(e.RolesJson) ?? [],
            IsActive = e.IsActive, CreatedAt = e.CreatedAt, ExpiresAt = e.ExpiresAt,
            LastUsedAt = e.LastUsedAt, PlainKey = plain
        };
}
