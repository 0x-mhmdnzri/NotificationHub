using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Layouts;

public interface ILayoutService
{
    Task<LayoutDefinition> SaveLayoutAsync(LayoutDefinition layout, CancellationToken ct = default);
    Task<PartialDefinition> SavePartialAsync(PartialDefinition partial, CancellationToken ct = default);
    Task<string> RenderHtmlAsync(string body, string? layoutKey, string? tenantId, IReadOnlyDictionary<string, object?> data, CancellationToken ct = default);
}
