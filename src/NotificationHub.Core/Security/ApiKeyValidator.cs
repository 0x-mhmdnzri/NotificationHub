using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Security;

public sealed class ApiKeyValidator : IApiKeyValidator
{
    private readonly IApiKeyStore _store;

    public ApiKeyValidator(IApiKeyStore store) => _store = store;

    public async Task<AuthContext?> ValidateAsync(string plainKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(plainKey)) return null;
        var hash = ApiKeyHasher.Hash(plainKey);
        var record = await _store.FindByHashAsync(hash, ct);
        if (record is null || !record.IsActive) return null;

        // fire-and-forget style touch; await for simplicity/correctness
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
