using System.Collections.Concurrent;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Templates;

public sealed class InMemoryTemplateStore : ITemplateStore
{
    private readonly ConcurrentDictionary<string, TemplateDefinition> _templates = new();

    private static string BuildKey(string key, string channel, string locale, string? tenantId)
        => $"{tenantId ?? "global"}:{channel}:{locale}:{key}".ToLowerInvariant();

    public Task SaveAsync(TemplateDefinition template, CancellationToken ct = default)
    {
        _templates[BuildKey(template.Key, template.Channel, template.Locale, template.TenantId)] = template;
        return Task.CompletedTask;
    }

    public Task<TemplateDefinition?> FindAsync(string key, string channel, string locale, string? tenantId, CancellationToken ct = default)
    {
        if (!string.IsNullOrEmpty(tenantId) &&
            _templates.TryGetValue(BuildKey(key, channel, locale, tenantId), out var tenant))
            return Task.FromResult<TemplateDefinition?>(tenant);

        if (_templates.TryGetValue(BuildKey(key, channel, locale, null), out var global))
            return Task.FromResult<TemplateDefinition?>(global);

        if (!string.Equals(locale, "en", StringComparison.OrdinalIgnoreCase) &&
            _templates.TryGetValue(BuildKey(key, channel, "en", null), out var en))
            return Task.FromResult<TemplateDefinition?>(en);

        return Task.FromResult<TemplateDefinition?>(null);
    }

    public Task<IReadOnlyList<TemplateDefinition>> ListAsync(string? tenantId = null, string? channel = null, CancellationToken ct = default)
    {
        IEnumerable<TemplateDefinition> q = _templates.Values.Where(x => x.IsActive);
        if (tenantId is not null) q = q.Where(x => x.TenantId == tenantId || x.TenantId is null);
        if (!string.IsNullOrEmpty(channel)) q = q.Where(x => x.Channel == channel);
        return Task.FromResult<IReadOnlyList<TemplateDefinition>>(q.ToList());
    }

    public Task<bool> DeleteAsync(string key, string channel, string locale, string? tenantId, CancellationToken ct = default)
    {
        var k = BuildKey(key, channel, locale, tenantId);
        return Task.FromResult(_templates.TryRemove(k, out _));
    }
}
