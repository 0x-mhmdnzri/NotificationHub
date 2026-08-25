using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Devices;

public interface IDeviceService
{
    Task<DeviceRegistration> RegisterAsync(RegisterDeviceRequest request, CancellationToken ct = default);
    Task<bool> UnregisterAsync(string userId, string token, string? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<DeviceRegistration>> ListAsync(string userId, string? tenantId, CancellationToken ct = default);
}
