using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Activity;

public interface IActivityService
{
    Task<IReadOnlyList<ActivityItem>> ListAsync(string? tenantId, int take = 50, CancellationToken ct = default);
}
