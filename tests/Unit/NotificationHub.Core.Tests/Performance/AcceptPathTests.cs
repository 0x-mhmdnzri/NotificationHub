using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Performance;

public class AcceptPathTests
{
    [Fact]
    public async Task TC_PERF_001_Accept_WithoutKeys_QueuesSuccessfully()
    {
        await using var db = TestFixtures.CreateDbContext();
        var orch = TestFixtures.CreateOrchestrator(db);
        var (ok, status) = await orch.AcceptAsync(new NotificationRequest
        {
            Recipient = "a@b.com",
            Channel = "email",
            TemplateKey = "welcome"
        });
        ok.Should().BeTrue();
        status.Status.Should().Be(DeliveryStatus.Queued);
    }

    [Fact]
    public async Task TC_PERF_002_Accept_Idempotent_ReturnsSame()
    {
        await using var db = TestFixtures.CreateDbContext();
        var orch = TestFixtures.CreateOrchestrator(db);
        var req = new NotificationRequest
        {
            Recipient = "a@b.com",
            Channel = "email",
            TemplateKey = "welcome",
            IdempotencyKey = "idem-1"
        };
        var (_, s1) = await orch.AcceptAsync(req);
        var (_, s2) = await orch.AcceptAsync(req);
        s2.NotificationId.Should().Be(s1.NotificationId);
    }
}
