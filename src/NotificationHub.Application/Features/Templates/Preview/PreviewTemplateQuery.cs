using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Templates.Preview;

public sealed record PreviewTemplateQuery(NotificationRequest Request, string? TrustedTenantId)
    : IQuery<Result<RenderedNotification>>;
