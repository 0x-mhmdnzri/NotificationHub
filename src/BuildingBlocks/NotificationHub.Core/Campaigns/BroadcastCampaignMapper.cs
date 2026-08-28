using System.Text.Json;
using NotificationHub.Core.Persistence;
using NotificationHub.Domain.Broadcast;
using NotificationHub.Domain.Broadcast.ValueObjects;
using NotificationHub.Domain.Common;
using NotificationHub.Domain.Delivery.ValueObjects;
using DomainStatus = NotificationHub.Domain.Broadcast.CampaignStatus;

namespace NotificationHub.Core.Campaigns;

/// <summary>Maps persistence entity ↔ Domain aggregate (keeps Core free of Infrastructure).</summary>
public static class BroadcastCampaignMapper
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static BroadcastCampaign ToDomain(BroadcastCampaignEntity e)
    {
        var channels = JsonSerializer.Deserialize<string[]>(e.ChannelsJson) ?? [];
        Dictionary<string, string>? data = null;
        if (e.DataJson is not null)
        {
            try { data = JsonSerializer.Deserialize<Dictionary<string, string>>(e.DataJson); }
            catch { /* ignore */ }
        }

        return BroadcastCampaign.Rehydrate(
            CampaignId.From(e.Id),
            e.Name,
            TenantId.From(e.TenantId),
            (DomainStatus)e.Status,
            TemplateKey.Create(string.IsNullOrEmpty(e.TemplateKey) ? "default" : e.TemplateKey),
            channels.Select(ChannelCode.Create),
            data,
            e.ScheduledAtUtc,
            e.CreatedAtUtc,
            e.StartedAtUtc,
            e.CompletedAtUtc,
            e.CreatedBy);
    }

    public static BroadcastCampaignEntity ToEntity(BroadcastCampaign c) => new()
    {
        Id = c.Id.Value,
        Name = c.Name,
        TenantId = c.TenantId?.Value,
        Status = (int)c.Status,
        TemplateKey = c.TemplateKey.Value,
        ChannelsJson = JsonSerializer.Serialize(c.Channels.Select(x => x.Value).ToArray()),
        DataJson = c.Data is null ? null : JsonSerializer.Serialize(c.Data),
        ScheduledAtUtc = c.ScheduledAtUtc,
        CreatedAtUtc = c.CreatedAtUtc,
        StartedAtUtc = c.StartedAtUtc,
        CompletedAtUtc = c.CompletedAtUtc,
        CreatedBy = c.CreatedBy
    };

    public static void Apply(BroadcastCampaign c, BroadcastCampaignEntity e)
    {
        e.Name = c.Name;
        e.Status = (int)c.Status;
        e.TemplateKey = c.TemplateKey.Value;
        e.ChannelsJson = JsonSerializer.Serialize(c.Channels.Select(x => x.Value).ToArray());
        e.DataJson = c.Data is null ? null : JsonSerializer.Serialize(c.Data);
        e.ScheduledAtUtc = c.ScheduledAtUtc;
        e.StartedAtUtc = c.StartedAtUtc;
        e.CompletedAtUtc = c.CompletedAtUtc;
    }
}
