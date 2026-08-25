using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Security;

/// <summary>Seeds bootstrap admin key from config when DB has no keys (SEC-01 / SEC-10).</summary>
public sealed class ApiKeyBootstrapper
{
    private static readonly HashSet<string> ForbiddenProductionKeys = new(StringComparer.Ordinal)
    {
        "dev-secret-key-change-me",
        "changeme",
        "secret",
        "password",
        "api-key",
        "apikey"
    };

    private readonly IApiKeyStore _store;
    private readonly IConfiguration _config;
    private readonly IHostEnvironment _env;
    private readonly ILogger<ApiKeyBootstrapper> _logger;

    public ApiKeyBootstrapper(
        IApiKeyStore store,
        IConfiguration config,
        IHostEnvironment env,
        ILogger<ApiKeyBootstrapper> logger)
    {
        _store = store;
        _config = config;
        _env = env;
        _logger = logger;
    }

    public async Task EnsureBootstrapKeyAsync(CancellationToken ct = default)
    {
        var existing = await _store.ListAsync(null, ct);
        if (existing.Count > 0) return;

        var configured = _config["Auth:BootstrapApiKey"] ?? _config["Auth:ApiKey"];
        string plain;
        string hash;

        if (string.IsNullOrWhiteSpace(configured))
        {
            if (_env.IsProduction())
            {
                throw new InvalidOperationException(
                    "Auth:BootstrapApiKey (or Auth:ApiKey) must be set via environment/secret when the database has no API keys.");
            }

            var id = Guid.NewGuid();
            plain = ApiKeyHasher.GeneratePlainKey(id);
            hash = ApiKeyHasher.Hash(plain);
            _logger.LogWarning(
                "No Auth:BootstrapApiKey configured. Generated one-time admin key (store it now): {Key}",
                plain);
        }
        else if (_env.IsProduction() && ForbiddenProductionKeys.Contains(configured.Trim()))
        {
            throw new InvalidOperationException(
                "Refusing to bootstrap with a known insecure Auth:BootstrapApiKey in Production.");
        }
        else
        {
            if (ForbiddenProductionKeys.Contains(configured.Trim()))
                _logger.LogWarning("Using a well-known development BootstrapApiKey. Never deploy this value to Production.");

            // Configured bootstrap may be legacy shape; store with PBKDF2 hash.
            // If it does not embed an id, CreateAsync assigns a new Guid id (validation uses legacy SHA256 path only if hash was legacy).
            // For configured secrets we always store v2 hash and require id-embedded key OR legacy lookup.
            // When configured key has no embedded id, also store a legacy hash lookup is impossible with PBKDF2 alone.
            // → Force generate id-embedded key when config is weak-dev style only; otherwise hash configured value as v2
            // and additionally store legacy hash is NOT possible in one column.
            // Practical approach: if TryParseKeyId fails, generate new id-embedded key and log that configured value is ignored in favor of generated (dev only),
            // OR hash configured with v2 and on validate also try FindByHash legacy - won't find.
            // Best: if configured has no embedded id, create key with new id and set plain to GeneratePlainKey(id), log both.
            if (ApiKeyHasher.TryParseKeyId(configured, out _))
            {
                plain = configured;
                hash = ApiKeyHasher.Hash(plain);
            }
            else if (_env.IsDevelopment())
            {
                // Keep exact configured secret for local DX: store as legacy SHA256 so Validate finds it.
                plain = configured;
                hash = ApiKeyHasher.HashLegacySha256(plain);
                _logger.LogWarning("Bootstrap key stored with legacy SHA256 hash for non-embedded local secret. Rotate to nh_{{guid}}_{{secret}} form for production.");
            }
            else
            {
                // Production: require embedded-id form or generate
                var id = Guid.NewGuid();
                plain = ApiKeyHasher.GeneratePlainKey(id);
                hash = ApiKeyHasher.Hash(plain);
                _logger.LogWarning(
                    "Configured BootstrapApiKey was not in nh_{{guid}}_{{secret}} form. Generated a strong key instead: {Key}",
                    plain);
            }
        }

        await _store.CreateAsync(new CreateApiKeyRequest
        {
            Name = "bootstrap-admin",
            TenantId = null,
            Roles = [AppRoles.Admin, AppRoles.Sender, AppRoles.Reader]
        }, plain, hash, ct);

        _logger.LogInformation("Bootstrap admin API key ensured");
    }
}
