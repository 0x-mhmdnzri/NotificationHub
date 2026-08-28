namespace NotificationHub.Host.Configuration;

/// <summary>
/// Resolves connection strings from appsettings / Aspire / environment.
/// Empty strings in base appsettings.json must not block env-specific or Aspire keys.
/// </summary>
public static class ConnectionStringResolver
{
    /// <summary>
    /// PostgreSQL keys used across local appsettings and Aspire AppHost
    /// (<c>AddDatabase("notificationdb")</c> → ConnectionStrings:notificationdb).
    /// </summary>
    private static readonly string[] PostgresKeys =
    [
        "Default",
        "notificationdb",
        "postgres",
        "NotificationDb",
        "PostgreSQL"
    ];

    private static readonly string[] RedisKeys =
    [
        "Redis",
        "redis"
    ];

    public static string? ResolvePostgres(IConfiguration configuration)
    {
        foreach (var key in PostgresKeys)
        {
            var value = configuration.GetConnectionString(key);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        // Flat / alternate config shapes
        foreach (var path in new[]
                 {
                     "ConnectionStrings:Default",
                     "ConnectionStrings:notificationdb",
                     "DATABASE_URL",
                     "Database:ConnectionString"
                 })
        {
            var value = configuration[path];
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        var env = Environment.GetEnvironmentVariable("ConnectionStrings__Default")
                  ?? Environment.GetEnvironmentVariable("ConnectionStrings__notificationdb")
                  ?? Environment.GetEnvironmentVariable("DATABASE_URL");
        return string.IsNullOrWhiteSpace(env) ? null : env.Trim();
    }

    public static string? ResolveRedis(IConfiguration configuration)
    {
        foreach (var key in RedisKeys)
        {
            var value = configuration.GetConnectionString(key);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        var value2 = configuration["ConnectionStrings:Redis"];
        if (!string.IsNullOrWhiteSpace(value2))
            return value2.Trim();

        var env = Environment.GetEnvironmentVariable("ConnectionStrings__Redis");
        return string.IsNullOrWhiteSpace(env) ? null : env.Trim();
    }

    /// <summary>Which configuration key provided the value (for diagnostics).</summary>
    public static string? PostgresSourceKey(IConfiguration configuration)
    {
        foreach (var key in PostgresKeys)
        {
            if (!string.IsNullOrWhiteSpace(configuration.GetConnectionString(key)))
                return $"ConnectionStrings:{key}";
        }

        if (!string.IsNullOrWhiteSpace(configuration["DATABASE_URL"]))
            return "DATABASE_URL";
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__Default")))
            return "env:ConnectionStrings__Default";
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DATABASE_URL")))
            return "env:DATABASE_URL";
        return null;
    }
}
