using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Security;

/// <summary>Seeds bootstrap admin key from config when DB has no keys (SRP).</summary>
public sealed class ApiKeyBootstrapper
{
    private readonly IApiKeyStore _store;
    private readonly IConfiguration _config;
    private readonly ILogger<ApiKeyBootstrapper> _logger;

    public ApiKeyBootstrapper(IApiKeyStore store, IConfiguration config, ILogger<ApiKeyBootstrapper> logger)
    {
        _store = store;
        _config = config;
        _logger = logger;
    }

    public async Task EnsureBootstrapKeyAsync(CancellationToken ct = default)
    {
        var existing = await _store.ListAsync(null, ct);
        if (existing.Count > 0) return;

        var plain = _config["Auth:BootstrapApiKey"] ?? _config["Auth:ApiKey"];
        if (string.IsNullOrWhiteSpace(plain))
        {
            plain = ApiKeyHasher.GeneratePlainKey();
            _logger.LogWarning("No Auth:BootstrapApiKey configured. Generated ephemeral admin key: {Key}", plain);
        }

        var hash = ApiKeyHasher.Hash(plain);
        await _store.CreateAsync(new CreateApiKeyRequest
        {
            Name = "bootstrap-admin",
            TenantId = null,
            Roles = [AppRoles.Admin, AppRoles.Sender, AppRoles.Reader]
        }, plain, hash, ct);

        _logger.LogInformation("Bootstrap admin API key ensured");
    }
}
