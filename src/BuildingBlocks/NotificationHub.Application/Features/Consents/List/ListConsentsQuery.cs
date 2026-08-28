using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Consents.List;

public sealed record ListConsentsQuery(string SubjectId, string? TrustedTenantId)
    : IQuery<Result<IReadOnlyList<ConsentRecord>>>;
