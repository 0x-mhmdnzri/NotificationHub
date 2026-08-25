using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Templates;

namespace NotificationHub.Core.Layouts;

/// <summary>F08/F09 — HTML layouts + partials; simple MJML-like {{content}} and {{>partial}}.</summary>
public sealed partial class LayoutService : ILayoutService
{
    private readonly NotificationDbContext _db;
    private readonly ITemplateRenderer _renderer;

    public LayoutService(NotificationDbContext db, ITemplateRenderer renderer)
    {
        _db = db;
        _renderer = renderer;
    }

    public async Task<LayoutDefinition> SaveLayoutAsync(LayoutDefinition layout, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(layout.Key) || layout.Key.Length > 128)
            throw new ArgumentException("Layout key required, max 128");
        if (string.IsNullOrWhiteSpace(layout.Html) || !layout.Html.Contains("{{content}}", StringComparison.Ordinal))
            throw new ArgumentException("Layout Html must contain {{content}} placeholder");
        if (layout.Html.Length > 500_000) throw new ArgumentException("Layout too large");

        var e = await _db.Layouts.FirstOrDefaultAsync(x => x.Key == layout.Key && x.TenantId == layout.TenantId, ct);
        if (e is null)
        {
            e = new LayoutEntity { Id = layout.Id == Guid.Empty ? Guid.NewGuid() : layout.Id };
            _db.Layouts.Add(e);
        }
        e.Key = layout.Key;
        e.TenantId = layout.TenantId;
        e.Html = layout.Html;
        e.Description = layout.Description;
        e.IsActive = layout.IsActive;
        await _db.SaveChangesAsync(ct);
        return ToLayout(e);
    }

    public async Task<PartialDefinition> SavePartialAsync(PartialDefinition partial, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(partial.Key)) throw new ArgumentException("Partial key required");
        var e = await _db.Partials.FirstOrDefaultAsync(x => x.Key == partial.Key && x.TenantId == partial.TenantId, ct);
        if (e is null)
        {
            e = new PartialEntity { Id = partial.Id == Guid.Empty ? Guid.NewGuid() : partial.Id };
            _db.Partials.Add(e);
        }
        e.Key = partial.Key;
        e.TenantId = partial.TenantId;
        e.Body = partial.Body;
        e.IsActive = partial.IsActive;
        await _db.SaveChangesAsync(ct);
        return new PartialDefinition { Id = e.Id, Key = e.Key, TenantId = e.TenantId, Body = e.Body, IsActive = e.IsActive };
    }

    public async Task<string> RenderHtmlAsync(string body, string? layoutKey, string? tenantId, IReadOnlyDictionary<string, object?> data, CancellationToken ct = default)
    {
        var withPartials = await ExpandPartialsAsync(body, tenantId, ct);
        var content = _renderer.Render(withPartials, data);

        if (string.IsNullOrWhiteSpace(layoutKey))
            return WrapMinimalHtml(content);

        var layout = await _db.Layouts.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Key == layoutKey && x.IsActive && (x.TenantId == tenantId || x.TenantId == null), ct);
        if (layout is null)
            return WrapMinimalHtml(content);

        var layoutBody = await ExpandPartialsAsync(layout.Html, tenantId, ct);
        var dataWithContent = new Dictionary<string, object?>(data) { ["content"] = content };
        // replace {{content}} literally then run placeholders
        var html = layoutBody.Replace("{{content}}", content, StringComparison.Ordinal);
        return _renderer.Render(html, dataWithContent);
    }

    private async Task<string> ExpandPartialsAsync(string input, string? tenantId, CancellationToken ct)
    {
        var result = input;
        foreach (Match m in PartialRegex().Matches(input))
        {
            var key = m.Groups[1].Value;
            var partial = await _db.Partials.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Key == key && x.IsActive && (x.TenantId == tenantId || x.TenantId == null), ct);
            result = result.Replace(m.Value, partial?.Body ?? "", StringComparison.Ordinal);
        }
        return result;
    }

    private static string WrapMinimalHtml(string content)
        => $"<!DOCTYPE html><html><body>{content}</body></html>";

    private static LayoutDefinition ToLayout(LayoutEntity e) => new()
    {
        Id = e.Id, Key = e.Key, TenantId = e.TenantId, Html = e.Html, Description = e.Description, IsActive = e.IsActive
    };

    [GeneratedRegex(@"\{\{>\s*([a-zA-Z0-9._\-]+)\s*\}\}", RegexOptions.Compiled)]
    private static partial Regex PartialRegex();
}
