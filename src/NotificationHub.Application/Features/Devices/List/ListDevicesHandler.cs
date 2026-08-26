using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Devices;

namespace NotificationHub.Application.Features.Devices.List;

public sealed class ListDevicesHandler(IDeviceService devices)
    : IRequestHandler<ListDevicesQuery, Result<IReadOnlyList<DeviceRegistration>>>
{
    public async Task<Result<IReadOnlyList<DeviceRegistration>>> Handle(ListDevicesQuery request, CancellationToken cancellationToken)
    {
        var list = await devices.ListAsync(request.UserId, request.TrustedTenantId, cancellationToken);
        return Result.Success(list);
    }
}
