using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Consents.Evaluate;

public sealed record EvaluateConsentQuery(
    string SubjectId, string Purpose, string? Channel, string? TrustedTenantId
) : IQuery<Result<ConsentDecision>>;
