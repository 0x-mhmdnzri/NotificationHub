namespace NotificationHub.Core.Auth;

/// <summary>JWT validation for human Admin API (alongside API Key — does not replace it).</summary>
public sealed class JwtBearerAuthOptions
{
    public const string SectionName = "Auth:JwtBearer";

    public bool Enabled { get; set; }
    public string Authority { get; set; } = "";
    public string Audience { get; set; } = "notificationhub-api";
    public bool RequireHttpsMetadata { get; set; } = true;
    /// <summary>Claim type for active organization id.</summary>
    public string TenantClaimType { get; set; } = "tenant_id";
}
