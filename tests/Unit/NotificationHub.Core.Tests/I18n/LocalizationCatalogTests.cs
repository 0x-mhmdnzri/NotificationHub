using FluentAssertions;
using NotificationHub.Core.I18n;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.I18n;

public class LocalizationCatalogTests
{
    [Fact]
    public async Task TC_F_I18N_001_SetAndGet()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new LocalizationCatalog(db);
        await sut.SetAsync("greeting", "fa", "سلام");
        (await sut.GetAsync("greeting", "fa")).Should().Be("سلام");
    }

    [Fact]
    public async Task TC_F_I18N_002_FallbackToEnglish()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new LocalizationCatalog(db);
        await sut.SetAsync("greeting", "en", "Hello");
        (await sut.GetAsync("greeting", "de")).Should().Be("Hello");
    }
}
