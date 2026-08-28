using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Topics;

namespace NotificationHub.Application.Features.Topics.List;

public sealed class ListTopicsHandler(ITopicService topics)
    : IRequestHandler<ListTopicsQuery, Result<IReadOnlyList<TopicDefinition>>>
{
    public async Task<Result<IReadOnlyList<TopicDefinition>>> Handle(ListTopicsQuery request, CancellationToken cancellationToken)
    {
        var list = await topics.ListTopicsAsync(request.TrustedTenantId, cancellationToken);
        return Result.Success(list);
    }
}
