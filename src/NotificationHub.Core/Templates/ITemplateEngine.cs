using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Templates;

public interface ITemplateEngine
{
    Task<RenderedNotification> RenderAsync(NotificationRequest request, CancellationToken ct = default);
    Task RegisterTemplateAsync(TemplateDefinition template, CancellationToken ct = default);
    Task<TemplateDefinition?> GetTemplateAsync(string key, string channel, string locale = "en", string? tenantId = null, CancellationToken ct = default);
}
