using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Consents.Record;

[AuthorizeRoles(AppRoles.Admin, AppRoles.Sender)]
public sealed record RecordConsentCommand(ConsentRecord Record, string? TrustedTenantId)
    : ICommand<Result<ConsentRecord>>;
