using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Devices.Register;

public sealed record RegisterDeviceCommand(RegisterDeviceRequest Request, string? TrustedTenantId)
    : ICommand<Result<DeviceRegistration>>;
