using System.Security.Cryptography;
using System.Text;

namespace NotificationHub.Core.Security;

/// <summary>
/// API key hashing (SEC-10).
/// New hashes: PBKDF2-SHA256 with unique salt (format v2.pbkdf2...).
/// Legacy SHA256 hex still verified for keys created before the upgrade.
/// </summary>
public static class ApiKeyHasher
{
    public const string V2Prefix = "v2.pbkdf2.";
    private const int Iterations = 100_000;
    private const int SaltSize = 16;
    private const int KeySize = 32;

    /// <summary>Produce a new salted PBKDF2 hash for storage.</summary>
    public static string Hash(string plainKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(plainKey);
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var derived = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plainKey),
            salt,
            Iterations,
            HashAlgorithmName.SHA256,
            KeySize);
        return $"{V2Prefix}{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(derived)}";
    }

    /// <summary>Legacy unsalted SHA256 (only for validating pre-upgrade rows).</summary>
    public static string HashLegacySha256(string plainKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static bool IsV2Hash(string stored) =>
        stored.StartsWith(V2Prefix, StringComparison.Ordinal);

    public static bool Verify(string plainKey, string storedHash)
    {
        if (string.IsNullOrEmpty(plainKey) || string.IsNullOrEmpty(storedHash))
            return false;

        if (IsV2Hash(storedHash))
            return VerifyPbkdf2(plainKey, storedHash);

        // Legacy: constant-time compare of SHA256 hex
        var legacy = HashLegacySha256(plainKey);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(legacy),
            Encoding.UTF8.GetBytes(storedHash.ToLowerInvariant()));
    }

    private static bool VerifyPbkdf2(string plainKey, string stored)
    {
        // v2.pbkdf2.{iterations}.{saltB64}.{hashB64}
        var parts = stored.Split('.', 5);
        if (parts.Length != 5) return false;
        if (!int.TryParse(parts[2], out var iterations) || iterations < 10_000) return false;
        byte[] salt, expected;
        try
        {
            salt = Convert.FromBase64String(parts[3]);
            expected = Convert.FromBase64String(parts[4]);
        }
        catch
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(plainKey),
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    /// <summary>
    /// Generates nh_{guid32}_{secret} so validators can load by id then Verify (O(1)).
    /// </summary>
    public static string GeneratePlainKey(Guid keyId)
    {
        var secret = RandomNumberGenerator.GetBytes(24);
        var secretPart = Convert.ToBase64String(secret)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
        return $"nh_{keyId:N}_{secretPart}";
    }

    /// <summary>Backward-compatible random key without embedded id (legacy shape).</summary>
    public static string GeneratePlainKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(48);
        var token = Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").Replace("=", "");
        if (token.Length > 40) token = token[..40];
        return "nh_" + token;
    }

    public static bool TryParseKeyId(string plainKey, out Guid keyId)
    {
        keyId = default;
        if (string.IsNullOrEmpty(plainKey) || !plainKey.StartsWith("nh_", StringComparison.Ordinal))
            return false;
        var rest = plainKey[3..];
        var underscore = rest.IndexOf('_');
        if (underscore != 32) return false; // Guid N format is 32 hex chars
        return Guid.TryParseExact(rest[..32], "N", out keyId);
    }
}
