using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Consents.Record;

public sealed record RecordConsentCommand(ConsentRecord Record, string? TrustedTenantId)
    : ICommand<Result<ConsentRecord>>;
