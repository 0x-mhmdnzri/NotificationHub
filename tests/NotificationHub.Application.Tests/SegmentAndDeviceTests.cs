using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.DependencyInjection;
using NotificationHub.Application.Features.Devices.Register;
using NotificationHub.Application.Features.Segments.Save;
using NotificationHub.Core.Devices;
using NotificationHub.Core.Segmentation;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Application.Tests;

public class SegmentAndDeviceTests
{
    [Fact]
    public async Task SaveSegment_Succeeds()
    {
        await using var db = TestFixtures.CreateDbContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<ISegmentService, SegmentService>();
        services.AddScoped(_ => db);
        services.AddSingleton(TestFixtures.CreateOrchestrator(db));
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        var result = await sender.Send(new SaveSegmentCommand(
            new SegmentDefinition { Key = "vip", MatchAll = true, Rules = [] }, null));
        result.IsSuccess.Should().BeTrue();
        result.Value!.Key.Should().Be("vip");
    }

    [Fact]
    public async Task RegisterDevice_Succeeds()
    {
        await using var db = TestFixtures.CreateDbContext();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddScoped<IDeviceService, DeviceService>();
        services.AddScoped(_ => db);
        services.AddSingleton(TestFixtures.CreateOrchestrator(db));
        var sender = services.BuildServiceProvider().GetRequiredService<ISender>();

        var result = await sender.Send(new RegisterDeviceCommand(
            new RegisterDeviceRequest { UserId = "u1", Token = "tok", Platform = "apns" }, null));
        result.IsSuccess.Should().BeTrue();
        result.Value!.Token.Should().Be("tok");
    }
}
