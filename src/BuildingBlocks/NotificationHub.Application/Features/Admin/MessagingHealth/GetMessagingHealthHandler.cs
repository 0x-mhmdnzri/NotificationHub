using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Messaging;

namespace NotificationHub.Application.Features.Admin.MessagingHealth;

public sealed class GetMessagingHealthHandler(IMessagingHealthService health)
    : IRequestHandler<GetMessagingHealthQuery, Result<MessagingHealthSnapshot>>
{
    public async Task<Result<MessagingHealthSnapshot>> Handle(GetMessagingHealthQuery request, CancellationToken cancellationToken)
    {
        var snap = await health.CheckAsync(cancellationToken);
        return Result.Success(snap);
    }
}
