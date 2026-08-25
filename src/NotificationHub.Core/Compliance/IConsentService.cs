using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Compliance;

/// <summary>Append-only consent ledger operations (SRP).</summary>
public interface IConsentService
{
    Task<ConsentRecord> RecordAsync(ConsentRecord record, CancellationToken ct = default);
    Task<ConsentDecision> EvaluateAsync(string subjectId, string purpose, string? channel = null, string? tenantId = null, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentRecord>> ListAsync(string subjectId, string? tenantId = null, CancellationToken ct = default);
}
