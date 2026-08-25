using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Security;

/// <summary>Persistence for API keys (SRP).</summary>
public interface IApiKeyStore
{
    Task<ApiKeyInfo> CreateAsync(CreateApiKeyRequest request, string plainKey, string keyHash, CancellationToken ct = default);
    Task<ApiKeyRecord?> FindByHashAsync(string keyHash, CancellationToken ct = default);
    Task TouchLastUsedAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKeyInfo>> ListAsync(string? tenantId = null, CancellationToken ct = default);
    Task<bool> RevokeAsync(Guid id, CancellationToken ct = default);
}

/// <summary>Internal record used by validator (includes hash).</summary>
public sealed class ApiKeyRecord
{
    public Guid Id { get; init; }
    public string Name { get; init; } = "";
    public string KeyHash { get; init; } = "";
    public string? TenantId { get; init; }
    public string[] Roles { get; init; } = [];
    public bool IsActive { get; init; }
    public DateTimeOffset? ExpiresAt { get; init; }
}
