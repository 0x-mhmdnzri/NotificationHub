using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Messaging;

namespace NotificationHub.Application.Features.Admin.MessagingHealth;

[AuthorizeRoles(AppRoles.Admin)]
public sealed record GetMessagingHealthQuery : IQuery<Result<MessagingHealthSnapshot>>;
