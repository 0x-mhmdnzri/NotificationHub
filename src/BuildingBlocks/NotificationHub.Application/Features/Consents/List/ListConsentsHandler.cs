using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Compliance;

namespace NotificationHub.Application.Features.Consents.List;

public sealed class ListConsentsHandler(IConsentService consents)
    : IRequestHandler<ListConsentsQuery, Result<IReadOnlyList<ConsentRecord>>>
{
    public async Task<Result<IReadOnlyList<ConsentRecord>>> Handle(ListConsentsQuery request, CancellationToken cancellationToken)
    {
        var list = await consents.ListAsync(request.SubjectId, request.TrustedTenantId, cancellationToken);
        return Result.Success(list);
    }
}
