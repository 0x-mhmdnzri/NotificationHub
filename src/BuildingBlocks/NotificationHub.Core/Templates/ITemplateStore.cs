using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Templates;

/// <summary>
/// Persistence concern only (SRP). Does not render templates.
/// </summary>
public interface ITemplateStore
{
    Task SaveAsync(TemplateDefinition template, CancellationToken ct = default);
    Task<TemplateDefinition?> FindAsync(string key, string channel, string locale, string? tenantId, CancellationToken ct = default);
    Task<IReadOnlyList<TemplateDefinition>> ListAsync(string? tenantId = null, string? channel = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(string key, string channel, string locale, string? tenantId, CancellationToken ct = default);
}
