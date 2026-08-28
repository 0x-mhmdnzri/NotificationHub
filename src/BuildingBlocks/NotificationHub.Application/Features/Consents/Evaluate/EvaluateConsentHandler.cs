using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Compliance;

namespace NotificationHub.Application.Features.Consents.Evaluate;

public sealed class EvaluateConsentHandler(IConsentService consents)
    : IRequestHandler<EvaluateConsentQuery, Result<ConsentDecision>>
{
    public async Task<Result<ConsentDecision>> Handle(EvaluateConsentQuery request, CancellationToken cancellationToken)
    {
        var decision = await consents.EvaluateAsync(
            request.SubjectId, request.Purpose, request.Channel, request.TrustedTenantId, cancellationToken);
        return Result.Success(decision);
    }
}
