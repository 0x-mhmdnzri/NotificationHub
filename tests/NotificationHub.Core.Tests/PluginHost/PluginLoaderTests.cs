using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NotificationHub.Abstractions.Plugins;
using NotificationHub.Core.PluginHost;

namespace NotificationHub.Core.Tests.PluginHost;

/// <summary>F20 — register / unregister.</summary>
public class PluginLoaderTests
{
    [Fact]
    public async Task TC_F_LOADER_001_Register_And_Unregister()
    {
        var loader = new PluginLoader(NullLogger<PluginLoader>.Instance);
        var mock = new Mock<IPlugin>();
        mock.SetupGet(x => x.Id).Returns("test-plugin");
        mock.SetupGet(x => x.Name).Returns("Test");
        mock.SetupGet(x => x.Version).Returns(new Version(1, 0, 0));
        mock.SetupGet(x => x.Capabilities).Returns(Array.Empty<PluginCapability>());
        mock.Setup(x => x.StopAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        loader.Register(mock.Object);
        loader.LoadedPlugins.Should().ContainSingle(p => p.Id == "test-plugin");

        await loader.UnregisterAsync("test-plugin");
        loader.LoadedPlugins.Should().BeEmpty();
        mock.Verify(x => x.StopAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public void TC_F_LOADER_002_DuplicateRegister_Ignored()
    {
        var loader = new PluginLoader(NullLogger<PluginLoader>.Instance);
        var mock = new Mock<IPlugin>();
        mock.SetupGet(x => x.Id).Returns("dup");
        mock.SetupGet(x => x.Name).Returns("D");
        mock.SetupGet(x => x.Version).Returns(new Version(1, 0, 0));
        mock.SetupGet(x => x.Capabilities).Returns(Array.Empty<PluginCapability>());
        loader.Register(mock.Object);
        loader.Register(mock.Object);
        loader.LoadedPlugins.Should().HaveCount(1);
    }
}
