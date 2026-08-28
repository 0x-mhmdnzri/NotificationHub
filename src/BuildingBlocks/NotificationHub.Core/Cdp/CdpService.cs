using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Workflow;

namespace NotificationHub.Core.Cdp;

public sealed class CdpService : ICdpService
{
    private readonly NotificationDbContext _db;
    private readonly IWorkflowEngine? _workflows;
    private readonly ILogger<CdpService> _logger;

    public CdpService(NotificationDbContext db, ILogger<CdpService> logger, IWorkflowEngine? workflows = null)
    {
        _db = db;
        _logger = logger;
        _workflows = workflows;
    }

    public async Task<CdpProfile> IdentifyAsync(CdpIdentifyRequest request, CancellationToken ct = default)
    {
        var e = await _db.CdpProfiles.FirstOrDefaultAsync(x => x.UserId == request.UserId && x.TenantId == request.TenantId, ct);
        if (e is null)
        {
            e = new CdpProfileEntity { UserId = request.UserId, TenantId = request.TenantId };
            _db.CdpProfiles.Add(e);
        }
        if (!string.IsNullOrWhiteSpace(request.Email)) e.Email = request.Email;
        if (!string.IsNullOrWhiteSpace(request.Phone)) e.Phone = request.Phone;
        var traits = string.IsNullOrEmpty(e.TraitsJson)
            ? new Dictionary<string, object?>()
            : JsonSerializer.Deserialize<Dictionary<string, object?>>(e.TraitsJson) ?? new();
        foreach (var kv in request.Traits)
            traits[kv.Key] = kv.Value;
        e.TraitsJson = JsonSerializer.Serialize(traits);
        e.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToProfile(e);
    }

    public async Task<(CdpProfile? Profile, Guid? WorkflowRunId, Guid? NotificationId)> TrackAsync(CdpTrackRequest request, CancellationToken ct = default)
    {
        _db.CdpEvents.Add(new CdpEventEntity
        {
            UserId = request.UserId,
            TenantId = request.TenantId,
            EventName = request.Event,
            PropertiesJson = JsonSerializer.Serialize(request.Properties)
        });
        await _db.SaveChangesAsync(ct);

        var profile = await GetProfileAsync(request.UserId, request.TenantId, ct);
        Guid? runId = null;
        if (!string.IsNullOrWhiteSpace(request.TriggerWorkflowKey) && _workflows is not null)
        {
            try
            {
                runId = await _workflows.StartAsync(new WorkflowStartRequest
                {
                    WorkflowKey = request.TriggerWorkflowKey!,
                    Recipient = profile?.Email ?? profile?.Phone ?? request.UserId,
                    TenantId = request.TenantId,
                    Data = request.Properties
                }, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CDP track failed to start workflow {Key}", request.TriggerWorkflowKey);
            }
        }

        return (profile, runId, null);
    }

    public async Task<CdpProfile?> GetProfileAsync(string userId, string? tenantId, CancellationToken ct = default)
    {
        var e = await _db.CdpProfiles.AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId && x.TenantId == tenantId, ct);
        return e is null ? null : ToProfile(e);
    }

    private static CdpProfile ToProfile(CdpProfileEntity e) => new()
    {
        UserId = e.UserId,
        TenantId = e.TenantId,
        Email = e.Email,
        Phone = e.Phone,
        Traits = JsonSerializer.Deserialize<Dictionary<string, object?>>(e.TraitsJson) ?? new(),
        UpdatedAt = e.UpdatedAt
    };
}
