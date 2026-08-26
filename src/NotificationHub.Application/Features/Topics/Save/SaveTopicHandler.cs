using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Topics;

namespace NotificationHub.Application.Features.Topics.Save;

public sealed class SaveTopicHandler(ITopicService topics)
    : IRequestHandler<SaveTopicCommand, Result<TopicDefinition>>
{
    public async Task<Result<TopicDefinition>> Handle(SaveTopicCommand request, CancellationToken cancellationToken)
    {
        var topic = request.Topic with { TenantId = request.TrustedTenantId ?? request.Topic.TenantId };
        var saved = await topics.SaveTopicAsync(topic, cancellationToken);
        return Result.Success(saved);
    }
}
