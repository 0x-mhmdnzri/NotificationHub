using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Cdp;

public interface ICdpService
{
    Task<CdpProfile> IdentifyAsync(CdpIdentifyRequest request, CancellationToken ct = default);
    Task<(CdpProfile? Profile, Guid? WorkflowRunId, Guid? NotificationId)> TrackAsync(CdpTrackRequest request, CancellationToken ct = default);
    Task<CdpProfile?> GetProfileAsync(string userId, string? tenantId, CancellationToken ct = default);
}
