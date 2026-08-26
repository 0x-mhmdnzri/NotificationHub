using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;

namespace NotificationHub.Application.Features.Devices.List;

public sealed record ListDevicesQuery(string UserId, string? TrustedTenantId)
    : IQuery<Result<IReadOnlyList<DeviceRegistration>>>;
