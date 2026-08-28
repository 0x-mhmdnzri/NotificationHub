using MediatR;
using NotificationHub.Abstractions.Models;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Devices;

namespace NotificationHub.Application.Features.Devices.Register;

public sealed class RegisterDeviceHandler(IDeviceService devices)
    : IRequestHandler<RegisterDeviceCommand, Result<DeviceRegistration>>
{
    public async Task<Result<DeviceRegistration>> Handle(RegisterDeviceCommand request, CancellationToken cancellationToken)
    {
        var req = request.Request;
        if (!string.IsNullOrEmpty(request.TrustedTenantId))
            req = req with { TenantId = request.TrustedTenantId };
        var reg = await devices.RegisterAsync(req, cancellationToken);
        return Result.Success(reg);
    }
}
