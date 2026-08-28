using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Common;
using NotificationHub.Core.Compliance;

namespace NotificationHub.Application.Features.Consents.Record;

public sealed class RecordConsentHandler(IConsentService consents)
    : IRequestHandler<RecordConsentCommand, Result<ConsentRecord>>
{
    public async Task<Result<ConsentRecord>> Handle(RecordConsentCommand request, CancellationToken cancellationToken)
    {
        var record = request.Record with
        {
            Id = ServerIds.New(),
            TenantId = request.TrustedTenantId ?? request.Record.TenantId
        };
        var saved = await consents.RecordAsync(record, cancellationToken);
        return Result.Success(saved);
    }
}
