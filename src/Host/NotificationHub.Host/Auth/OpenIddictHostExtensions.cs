namespace NotificationHub.Host.Auth;

/// <summary>
/// OpenIddict packages remain referenced for future token-endpoint expansion.
/// Host login/register currently issues HS256 JWTs via <see cref="AccountAuthService"/>;
/// EF-backed OpenIddict stores are not registered on <c>NotificationDbContext</c>
/// (avoids PendingModelChangesWarning against the existing MigrateAsync + SQL schema path).
/// </summary>
public static class OpenIddictHostExtensions
{
    // Intentionally empty registration surface for Host until a dedicated OpenIddict DbContext/migration exists.
}
