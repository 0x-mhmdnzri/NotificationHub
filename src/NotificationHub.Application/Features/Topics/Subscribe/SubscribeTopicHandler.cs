using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Topics;

namespace NotificationHub.Application.Features.Topics.Subscribe;

public sealed class SubscribeTopicHandler(ITopicService topics)
    : IRequestHandler<SubscribeTopicCommand, Result>
{
    public async Task<Result> Handle(SubscribeTopicCommand request, CancellationToken cancellationToken)
    {
        await topics.SubscribeAsync(request.TopicKey, request.SubscriberId, request.TrustedTenantId, request.Channel, request.Address, cancellationToken);
        return Result.Success();
    }
}
