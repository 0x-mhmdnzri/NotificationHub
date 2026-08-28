using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Devices;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Devices;

/// <summary>F05 — device token registry.</summary>
public class DeviceServiceTests
{
    [Fact]
    public async Task TC_F_DEV_001_RegisterAndList()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new DeviceService(db);
        await sut.RegisterAsync(new RegisterDeviceRequest { UserId = "u1", Platform = "fcm", Token = "tok-abc" });
        var list = await sut.ListAsync("u1", null);
        list.Should().HaveCount(1);
        list[0].Platform.Should().Be("fcm");
    }

    [Fact]
    public async Task TC_ERR_DEV_002_InvalidPlatform()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new DeviceService(db);
        var act = () => sut.RegisterAsync(new RegisterDeviceRequest { UserId = "u", Platform = "blackberry", Token = "x" });
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task TC_F_DEV_003_Unregister()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new DeviceService(db);
        await sut.RegisterAsync(new RegisterDeviceRequest { UserId = "u1", Platform = "apns", Token = "t1" });
        (await sut.UnregisterAsync("u1", "t1", null)).Should().BeTrue();
        (await sut.ListAsync("u1", null)).Should().BeEmpty();
    }

    [Fact]
    public async Task TC_E_DEV_004_ReRegister_Reactivates()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = new DeviceService(db);
        await sut.RegisterAsync(new RegisterDeviceRequest { UserId = "u1", Platform = "webpush", Token = "w1" });
        await sut.UnregisterAsync("u1", "w1", null);
        await sut.RegisterAsync(new RegisterDeviceRequest { UserId = "u1", Platform = "webpush", Token = "w1" });
        (await sut.ListAsync("u1", null)).Should().HaveCount(1);
    }
}
