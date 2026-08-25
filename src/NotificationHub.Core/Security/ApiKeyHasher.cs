using System.Security.Cryptography;
using System.Text;

namespace NotificationHub.Core.Security;

/// <summary>Hashing only (SRP). Never store plain keys.</summary>
public static class ApiKeyHasher
{
    public static string Hash(string plainKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(plainKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GeneratePlainKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "nh_" + Convert.ToBase64String(bytes).Replace("+", "").Replace("/", "").Replace("=", "")[..40];
    }
}
