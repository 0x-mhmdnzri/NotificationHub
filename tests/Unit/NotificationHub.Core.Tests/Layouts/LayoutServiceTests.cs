using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Layouts;
using NotificationHub.Core.Templates;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Layouts;

/// <summary>F08/F09 — layouts and partials.</summary>
public class LayoutServiceTests
{
    [Fact]
    public async Task TC_F_LAY_001_RenderWithLayoutAndContent()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new LayoutService(db, new PlaceholderTemplateRenderer());
        await sut.SaveLayoutAsync(new LayoutDefinition
        {
            Key = "base",
            Html = "<html><body><header>H</header>{{content}}</body></html>"
        });
        var html = await sut.RenderHtmlAsync("<p>{{name}}</p>", "base", null, new Dictionary<string, object?> { ["name"] = "Ada" });
        html.Should().Contain("Ada");
        html.Should().Contain("<header>H</header>");
    }

    [Fact]
    public async Task TC_F_LAY_002_PartialExpansion()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new LayoutService(db, new PlaceholderTemplateRenderer());
        await sut.SavePartialAsync(new PartialDefinition { Key = "footer", Body = "Bye {{name}}" });
        var html = await sut.RenderHtmlAsync("Hi {{>footer}}", null, null, new Dictionary<string, object?> { ["name"] = "Ada" });
        html.Should().Contain("Bye Ada");
    }

    [Fact]
    public async Task TC_ERR_LAY_003_LayoutWithoutContent_Throws()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new LayoutService(db, new PlaceholderTemplateRenderer());
        var act = () => sut.SaveLayoutAsync(new LayoutDefinition { Key = "x", Html = "<div/>" });
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
