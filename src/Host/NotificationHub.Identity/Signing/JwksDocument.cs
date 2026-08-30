using System.Security.Cryptography;
using System.Text.Json.Serialization;

namespace NotificationHub.Identity.Signing;

public sealed class JwksDocument
{
    [JsonPropertyName("keys")]
    public List<Jwk> Keys { get; set; } = [];
}

public sealed class Jwk
{
    [JsonPropertyName("kty")] public string Kty { get; set; } = "RSA";
    [JsonPropertyName("use")] public string Use { get; set; } = "sig";
    [JsonPropertyName("kid")] public string Kid { get; set; } = "";
    [JsonPropertyName("alg")] public string Alg { get; set; } = "RS256";
    [JsonPropertyName("n")] public string N { get; set; } = "";
    [JsonPropertyName("e")] public string E { get; set; } = "";
}

public static class RsaJwksFactory
{
    public static JwksDocument FromRsa(RSA rsa, string keyId)
    {
        var p = rsa.ExportParameters(false);
        return new JwksDocument
        {
            Keys =
            [
                new Jwk
                {
                    Kid = keyId,
                    N = Base64Url(p.Modulus!),
                    E = Base64Url(p.Exponent!)
                }
            ]
        };
    }

    static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
