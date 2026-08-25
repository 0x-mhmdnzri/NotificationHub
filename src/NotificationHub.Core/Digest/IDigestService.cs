using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Digest;

public interface IDigestService
{
    Task<DigestPolicy> SavePolicyAsync(DigestPolicy policy, CancellationToken ct = default);
    Task BufferAsync(string policyKey, string recipient, string? tenantId, object payload, CancellationToken ct = default);
    /// <summary>Flushes due buffers and enqueues digest notifications (F31).</summary>
    Task<int> FlushDueAsync(CancellationToken ct = default);
}
