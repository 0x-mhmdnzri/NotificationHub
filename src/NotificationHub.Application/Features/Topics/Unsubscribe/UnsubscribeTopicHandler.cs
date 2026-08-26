using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Topics;

namespace NotificationHub.Application.Features.Topics.Unsubscribe;

public sealed class UnsubscribeTopicHandler(ITopicService topics)
    : IRequestHandler<UnsubscribeTopicCommand, Result>
{
    public async Task<Result> Handle(UnsubscribeTopicCommand request, CancellationToken cancellationToken)
    {
        await topics.UnsubscribeAsync(request.TopicKey, request.SubscriberId, request.TrustedTenantId, cancellationToken);
        return Result.Success();
    }
}
