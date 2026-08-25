using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NotificationHub.Abstractions.Channels;
using NotificationHub.Abstractions.Models;
using NotificationHub.Abstractions.Plugins;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Plugins.InApp;

public sealed class InAppPlugin : IChannelPlugin
{
    private ILogger? _logger;
    private IServiceProvider? _services;
    public string Id => "inapp-inbox";
    public Version Version => new(1, 0, 0);
    public string Name => "In-App Inbox";
    public string Channel => "inapp";
    public PluginCapability[] Capabilities => [new("channel", "inapp"), new("persistent", "true")];
    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken = default)
    { _logger = context.Logger; _services = context.Services; return Task.CompletedTask; }
    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task<PluginHealth> HealthCheckAsync(CancellationToken cancellationToken = default) => Task.FromResult(new PluginHealth(true, "OK"));
    public async Task<DeliveryResult> SendAsync(RenderedNotification notification, CancellationToken cancellationToken = default)
    {
        if (_services is null) return new DeliveryResult { Success = false, ErrorCode = "NO_SERVICES", ErrorMessage = "DI not available" };
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
        var entity = new InAppMessageEntity { Id = Guid.NewGuid(), UserId = notification.Recipient, TenantId = notification.TenantId, Title = notification.Subject, Body = notification.Body, IsRead = false, CreatedAt = DateTimeOffset.UtcNow };
        db.InAppMessages.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return new DeliveryResult { Success = true, ProviderMessageId = entity.Id.ToString() };
    }
}
