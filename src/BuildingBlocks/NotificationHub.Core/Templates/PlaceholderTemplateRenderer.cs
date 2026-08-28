using System.Text.RegularExpressions;

namespace NotificationHub.Core.Templates;

/// <summary>
/// Default {{placeholder}} renderer. Open for extension via ITemplateRenderer (OCP).
/// </summary>
public sealed class PlaceholderTemplateRenderer : ITemplateRenderer
{
    private static readonly Regex PlaceholderRegex = new(@"\{\{(\w+)\}\}", RegexOptions.Compiled);

    public string Render(string template, IReadOnlyDictionary<string, object?> data)
    {
        return PlaceholderRegex.Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return data.TryGetValue(key, out var value) ? value?.ToString() ?? string.Empty : match.Value;
        });
    }
}
