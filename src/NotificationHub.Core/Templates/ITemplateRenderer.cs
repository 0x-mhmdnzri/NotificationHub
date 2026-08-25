namespace NotificationHub.Core.Templates;

/// <summary>
/// Rendering concern only (SRP). Does not load templates from storage.
/// </summary>
public interface ITemplateRenderer
{
    string Render(string template, IReadOnlyDictionary<string, object?> data);
}
