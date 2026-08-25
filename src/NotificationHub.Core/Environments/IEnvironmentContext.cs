namespace NotificationHub.Core.Environments;

public interface IEnvironmentContext
{
    string Name { get; } // development | staging | production
    bool IsProduction { get; }
    bool AllowDangerousOperations { get; }
    string? PrefixKey(string key);
}

public sealed class EnvironmentContext : IEnvironmentContext
{
    public EnvironmentContext(Microsoft.Extensions.Configuration.IConfiguration config, Microsoft.Extensions.Hosting.IHostEnvironment env)
    {
        Name = config["NotificationHub:Environment"]
            ?? env.EnvironmentName
            ?? "Development";
        IsProduction = string.Equals(Name, "Production", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Name, "prod", StringComparison.OrdinalIgnoreCase);
        AllowDangerousOperations = !IsProduction;
    }

    public string Name { get; }
    public bool IsProduction { get; }
    public bool AllowDangerousOperations { get; }
    public string? PrefixKey(string key) => string.IsNullOrEmpty(key) ? key : $"{Name.ToLowerInvariant()}:{key}";
}
