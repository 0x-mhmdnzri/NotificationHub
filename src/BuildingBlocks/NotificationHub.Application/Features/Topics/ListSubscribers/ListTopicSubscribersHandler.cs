using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Topics;

namespace NotificationHub.Application.Features.Topics.ListSubscribers;

public sealed class ListTopicSubscribersHandler(ITopicService topics)
    : IRequestHandler<ListTopicSubscribersQuery, Result<IReadOnlyList<TopicSubscriber>>>
{
    public async Task<Result<IReadOnlyList<TopicSubscriber>>> Handle(ListTopicSubscribersQuery request, CancellationToken cancellationToken)
    {
        var list = await topics.ListSubscribersAsync(request.TopicKey, request.TrustedTenantId, cancellationToken);
        return Result.Success(list);
    }
}
