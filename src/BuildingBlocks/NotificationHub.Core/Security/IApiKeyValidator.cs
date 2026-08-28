using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Security;

/// <summary>Validates presented API keys (SRP).</summary>
public interface IApiKeyValidator
{
    Task<AuthContext?> ValidateAsync(string plainKey, CancellationToken ct = default);
}
