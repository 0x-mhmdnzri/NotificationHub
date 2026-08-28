using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Devices.Unregister;

public sealed record UnregisterDeviceCommand(string UserId, string Token, string? TrustedTenantId)
    : ICommand<Result>;
