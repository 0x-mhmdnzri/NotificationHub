using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Messaging;

namespace NotificationHub.Application.Features.Admin.MessagingHealth;

public sealed record GetMessagingHealthQuery : IQuery<Result<MessagingHealthSnapshot>>;
