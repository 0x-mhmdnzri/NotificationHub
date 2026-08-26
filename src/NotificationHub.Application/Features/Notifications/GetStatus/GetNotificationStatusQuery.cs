using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Notifications.GetStatus;

public sealed record GetNotificationStatusQuery(
    Guid NotificationId,
    string? TrustedTenantId,
    bool IsAdmin
) : IQuery<Result<NotificationStatusDto>>;

public sealed record NotificationStatusDto(
    Guid NotificationId,
    string Channel,
    string Recipient,
    string Status,
    string? ProviderId,
    string? ErrorCode,
    string? ErrorMessage,
    int AttemptCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? TenantId,
    string? CorrelationId,
    string? Category
);
