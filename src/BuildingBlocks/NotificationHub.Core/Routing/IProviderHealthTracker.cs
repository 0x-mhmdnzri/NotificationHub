using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Routing;

/// <summary>Records provider outcomes and exposes health scores (SRP).</summary>
public interface IProviderHealthTracker
{
    void RecordSuccess(string providerId, string channel);
    void RecordFailure(string providerId, string channel, string? errorCode = null);
    ProviderHealthSnapshot GetHealth(string providerId, string channel);
    IReadOnlyList<ProviderHealthSnapshot> GetAll();
}
