namespace NotificationHub.Identity.Signing;

/// <summary>
/// Signing key configuration for OpenIddict / JWT (A2.7 JWKS readiness).
/// Production: load RSA from Key Vault / file; Development: ephemeral RSA.
/// </summary>
public sealed class SigningKeyOptions
{
    public const string SectionName = "Identity:Signing";

    /// <summary>PEM or base64 RSA private key. Empty → generate ephemeral (dev only).</summary>
    public string? RsaPrivateKeyPem { get; set; }

    /// <summary>Key id exposed on JWKS.</summary>
    public string KeyId { get; set; } = "nh-signing-1";

    /// <summary>When true, host exposes /.well-known/jwks.json for API validation.</summary>
    public bool ExposeJwks { get; set; } = true;
}
