using MediatR;
using NotificationHub.Application.Abstractions;
using NotificationHub.Core.Devices;

namespace NotificationHub.Application.Features.Devices.Unregister;

public sealed class UnregisterDeviceHandler(IDeviceService devices)
    : IRequestHandler<UnregisterDeviceCommand, Result>
{
    public async Task<Result> Handle(UnregisterDeviceCommand request, CancellationToken cancellationToken)
    {
        var ok = await devices.UnregisterAsync(request.UserId, request.Token, request.TrustedTenantId, cancellationToken);
        return ok ? Result.Success() : Result.Failure(Error.NotFound("device.not_found", "Device not found."));
    }
}
