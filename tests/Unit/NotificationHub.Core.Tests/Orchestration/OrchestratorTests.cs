using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Preferences;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Orchestration;

public class OrchestratorTests
{
    [Fact]
    public async Task TC_F_040_Accept_QueuesNotification()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = TestFixtures.CreateOrchestrator(db, TestFixtures.CreateChannelPlugin("email-sendgrid", "email"));

        var request = new NotificationRequest
        {
            Recipient = "a@b.com",
            Channel = "email",
            TemplateKey = "welcome",
            Data = new Dictionary<string, object?> { ["name"] = "Ali" }
        };

        var (accepted, status) = await sut.AcceptAsync(request);

        accepted.Should().BeTrue();
        status.Status.Should().Be(DeliveryStatus.Queued);
        status.Channel.Should().Be("email");
    }

    [Fact]
    public async Task TC_F_041_Accept_Idempotent_ReturnsSame()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = TestFixtures.CreateOrchestrator(db, TestFixtures.CreateChannelPlugin("email-sendgrid", "email"));

        var request = new NotificationRequest
        {
            Recipient = "a@b.com",
            Channel = "email",
            TemplateKey = "welcome",
            IdempotencyKey = "welcome-1",
            Data = new()
        };

        var first = await sut.AcceptAsync(request);
        var second = await sut.AcceptAsync(request with { Id = Guid.NewGuid() });

        first.Status.NotificationId.Should().Be(second.Status.NotificationId);
    }

    [Fact]
    public async Task TC_F_042_Process_Success_SendsViaPreferredProvider()
    {
        await using var db = TestFixtures.CreateDbContext();
        var plugin = TestFixtures.CreateChannelPlugin("email-sendgrid", "email");
        var sut = TestFixtures.CreateOrchestrator(db, plugin);

        var request = new NotificationRequest
        {
            Recipient = "a@b.com",
            Channel = "email",
            TemplateKey = "welcome",
            Data = new Dictionary<string, object?> { ["name"] = "Ali" }
        };
        await sut.AcceptAsync(request);
        var result = await sut.ProcessAsync(request);

        result.Success.Should().BeTrue();
        result.ProviderId.Should().Be("email-sendgrid");
    }

    [Fact]
    public async Task TC_F_043_Process_Failover_UsesNextProvider()
    {
        await using var db = TestFixtures.CreateDbContext();
        var primary = TestFixtures.CreateChannelPlugin("email-sendgrid", "email", success: false, errorCode: "DOWN");
        var secondary = TestFixtures.CreateChannelPlugin("email-smtp", "email", success: true);
        var sut = TestFixtures.CreateOrchestrator(db, primary, secondary);

        var request = new NotificationRequest
        {
            Recipient = "a@b.com",
            Channel = "email",
            TemplateKey = "welcome",
            AllowFallback = true,
            Data = new Dictionary<string, object?> { ["name"] = "Ali" }
        };
        await sut.AcceptAsync(request);
        var result = await sut.ProcessAsync(request);

        result.Success.Should().BeTrue();
        result.ProviderId.Should().Be("email-smtp");
    }

    [Fact]
    public async Task TC_ERR_040_Process_NoPlugin_Fails()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = TestFixtures.CreateOrchestrator(db);

        var request = new NotificationRequest
        {
            Recipient = "a@b.com",
            Channel = "push",
            TemplateKey = "welcome",
            Data = new()
        };
        await sut.AcceptAsync(request);
        var result = await sut.ProcessAsync(request);

        result.Success.Should().BeFalse();
        result.ErrorCode.Should().Be("NO_PLUGIN");
    }

    [Fact]
    public async Task TC_ST_010_Accept_OptOut_Suppresses()
    {
        await using var db = TestFixtures.CreateDbContext();
        var prefs = new PreferenceService(db);
        await prefs.SaveAsync(new UserPreference
        {
            UserId = "blocked@x.com",
            ChannelOptIn = new Dictionary<string, bool> { ["email"] = false }
        });

        var sut = TestFixtures.CreateOrchestrator(db, TestFixtures.CreateChannelPlugin("email-sendgrid", "email"));
        var request = new NotificationRequest
        {
            Recipient = "blocked@x.com",
            Channel = "email",
            TemplateKey = "welcome",
            Data = new()
        };

        var (accepted, status) = await sut.AcceptAsync(request);

        accepted.Should().BeTrue();
        status.Status.Should().Be(DeliveryStatus.Suppressed);
    }

    [Fact]
    public async Task TC_ST_011_Accept_Scheduled_SetsScheduledStatus()
    {
        await using var db = TestFixtures.CreateDbContext();
        var sut = TestFixtures.CreateOrchestrator(db, TestFixtures.CreateChannelPlugin("sms-kavenegar", "sms"));

        var request = new NotificationRequest
        {
            Recipient = "+98912",
            Channel = "sms",
            TemplateKey = "otp",
            ScheduledAt = DateTimeOffset.UtcNow.AddHours(2),
            Data = new Dictionary<string, object?> { ["code"] = "1111", ["minutes"] = "5" }
        };

        var (_, status) = await sut.AcceptAsync(request);
        status.Status.Should().Be(DeliveryStatus.Scheduled);
        status.ScheduledAt.Should().NotBeNull();
    }
}
