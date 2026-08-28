using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Compliance;

/// <summary>Applies retention policies and purges expired data (SRP).</summary>
public interface IRetentionService
{
    Task<RetentionSweepResult> SweepAsync(CancellationToken ct = default);
}
