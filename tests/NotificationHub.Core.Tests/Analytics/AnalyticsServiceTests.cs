using FluentAssertions;
using Microsoft.Extensions.Options;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Analytics;
using NotificationHub.Core.Store;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Analytics;

public class AnalyticsServiceTests
{
    [Fact]
    public async Task TC_F_060_Analytics_ComputesRatesAndCost()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresNotificationStatusStore(db);
        await store.SaveAsync(new NotificationStatus { NotificationId = Guid.NewGuid(), Channel = "sms", Recipient = "1", Status = DeliveryStatus.Sent, ProviderId = "sms-kavenegar" });
        await store.SaveAsync(new NotificationStatus { NotificationId = Guid.NewGuid(), Channel = "sms", Recipient = "2", Status = DeliveryStatus.Failed, ProviderId = "sms-kavenegar" });

        var sut = new AnalyticsService(db, Options.Create(new CostOptions
        {
            Providers = [new ProviderCostConfig { ProviderId = "sms-kavenegar", CostPerMessage = 200 }]
        }));

        var summary = await sut.GetSummaryAsync();
        summary.Total.Should().Be(2);
        summary.Sent.Should().Be(1);
        summary.Failed.Should().Be(1);
        summary.DeliveryRate.Should().Be(0.5);
        summary.EstimatedCost.Should().Be(200);
    }
}
