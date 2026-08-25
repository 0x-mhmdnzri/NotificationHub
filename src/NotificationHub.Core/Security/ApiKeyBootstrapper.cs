using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Security;

/// <summary>Seeds bootstrap admin key from config when DB has no keys (SEC-01).</summary>
public sealed class ApiKeyBootstrapper
{
    /// <summary>Known insecure defaults that must never be used outside local dev.</summary>
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

        var plain = _config["Auth:BootstrapApiKey"] ?? _config["Auth:ApiKey"];

        if (string.IsNullOrWhiteSpace(plain))
        {
            if (_env.IsProduction())
            {
                throw new InvalidOperationException(
                    "Auth:BootstrapApiKey (or Auth:ApiKey) must be set via environment/secret when the database has no API keys. " +
                    "Do not rely on a committed default.");
            }

            plain = ApiKeyHasher.GeneratePlainKey();
            _logger.LogWarning(
                "No Auth:BootstrapApiKey configured. Generated one-time admin key (store it now; it will not be shown again): {Key}",
                plain);
        }
        else if (_env.IsProduction() && ForbiddenProductionKeys.Contains(plain.Trim()))
        {
            throw new InvalidOperationException(
                "Refusing to bootstrap with a known insecure Auth:BootstrapApiKey in Production. " +
                "Set a strong unique key via environment variable Auth__BootstrapApiKey.");
        }
        else if (ForbiddenProductionKeys.Contains(plain.Trim()))
        {
            _logger.LogWarning(
                "Using a well-known development BootstrapApiKey. Never deploy this value to Production.");
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
