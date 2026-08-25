namespace NotificationHub.Core.Auth;

/// <summary>F30 — OIDC/SSO settings for future dashboard (not forced on API key host).</summary>
public sealed class OidcOptions
{
    public const string SectionName = "Auth:Oidc";
    public bool Enabled { get; set; }
    public string? Authority { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? Audience { get; set; }
    public string[] Scopes { get; set; } = ["openid", "profile", "email"];
}
