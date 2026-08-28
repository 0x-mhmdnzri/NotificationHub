using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using NotificationHub.Core.Environments;

namespace NotificationHub.Core.Tests.Environments;

public class EnvironmentContextTests
{
    [Fact]
    public void TC_F_ENV_001_Production_DisallowsDangerous()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["NotificationHub:Environment"] = "Production"
        }).Build();
        var env = new EnvironmentContext(config, new StubHostEnv("Production"));
        env.IsProduction.Should().BeTrue();
        env.AllowDangerousOperations.Should().BeFalse();
        env.PrefixKey("k").Should().Be("production:k");
    }

    [Fact]
    public void TC_F_ENV_002_Development_AllowsDangerous()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var env = new EnvironmentContext(config, new StubHostEnv("Development"));
        env.IsProduction.Should().BeFalse();
        env.AllowDangerousOperations.Should().BeTrue();
    }

    private sealed class StubHostEnv : IHostEnvironment
    {
        public StubHostEnv(string name) => EnvironmentName = name;
        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = "/";
        public IFileProvider ContentRootFileProvider { get; set; } = null!;
    }
}
