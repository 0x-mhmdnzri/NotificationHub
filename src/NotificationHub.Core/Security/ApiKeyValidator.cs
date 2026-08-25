using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Security;

public sealed class ApiKeyValidator : IApiKeyValidator
{
    private readonly IApiKeyStore _store;

    public ApiKeyValidator(IApiKeyStore store) => _store = store;

    public async Task<AuthContext?> ValidateAsync(string plainKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plainKey)) return null;

        ApiKeyRecord? record = null;

        // Preferred path: key embeds id → load + Verify (PBKDF2)
        if (ApiKeyHasher.TryParseKeyId(plainKey, out var keyId))
        {
            record = await _store.FindByIdAsync(keyId, ct);
            if (record is null || !record.IsActive) return null;
            if (!ApiKeyHasher.Verify(plainKey, record.KeyHash)) return null;
        }
        else
        {
            // Legacy path: SHA256 lookup then verify (covers pre-P1 keys)
            var legacyHash = ApiKeyHasher.HashLegacySha256(plainKey);
            record = await _store.FindByHashAsync(legacyHash, ct);
            if (record is null) return null;
            if (!ApiKeyHasher.Verify(plainKey, record.KeyHash)) return null;
        }

        if (record.ExpiresAt.HasValue && record.ExpiresAt < DateTimeOffset.UtcNow)
            return null;

        await _store.TouchLastUsedAsync(record.Id, ct);

        return new AuthContext
        {
            ApiKeyId = record.Id,
            TenantId = record.TenantId,
            Roles = record.Roles,
            KeyName = record.Name
        };
    }
}
