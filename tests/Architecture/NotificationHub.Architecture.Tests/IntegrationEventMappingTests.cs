using NotificationHub.Abstractions.IntegrationEvents;
using NotificationHub.Domain.Delivery.Events;
using NotificationHub.Domain.Delivery.ValueObjects;
using NotificationHub.Infrastructure.Messaging.Integration;
using Xunit;

namespace NotificationHub.Architecture.Tests;

public class IntegrationEventMappingTests
{
    [Fact]
    public void Domain_Accepted_maps_to_NotificationAcceptedV1()
    {
        var e = new NotificationAccepted(
            NotificationId.New(),
            RecipientAddress.Create("a@b.com"),
            ChannelCode.Create("email"),
            TemplateKey.Create("t1"),
            "tenant-1",
            DateTimeOffset.UtcNow);

        var env = DomainEventToIntegrationMapper.TryMap(e);
        Assert.NotNull(env);
        Assert.Equal("notification.accepted", env!.EventType);
        Assert.Equal(1, env.Version);
        Assert.IsType<NotificationAcceptedV1>(env.Payload);
    }

    [Fact]
    public void Domain_MarkedProcessing_is_not_published_as_integration()
    {
        var e = new NotificationMarkedProcessing(NotificationId.New(), DateTimeOffset.UtcNow);
        Assert.Null(DomainEventToIntegrationMapper.TryMap(e));
    }

    [Fact]
    public void Domain_Suppressed_maps_to_V1_with_reason()
    {
        var e = new NotificationSuppressed(
            NotificationId.New(),
            RecipientAddress.Create("a@b.com"),
            ChannelCode.Create("sms"),
            "consent_denied",
            "t1",
            DateTimeOffset.UtcNow);
        var env = DomainEventToIntegrationMapper.TryMap(e);
        Assert.NotNull(env);
        var p = Assert.IsType<NotificationSuppressedV1>(env!.Payload);
        Assert.Equal("consent_denied", p.Reason);
        Assert.Equal("notification.suppressed", env.EventType);
    }
}
