using System.Text.Json;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Workflow;

/// <summary>F07 — versioned workflow document for export/import (code-friendly DSL as JSON).</summary>
public sealed record WorkflowDocument
{
    public string SchemaVersion { get; init; } = "1.0";
    public required WorkflowDefinition Definition { get; init; }
}

public static class WorkflowDsl
{
    private static readonly HashSet<string> KnownSteps = new(StringComparer.OrdinalIgnoreCase)
    {
        "delay", "condition", "branch", "send", "http"
    };

    public static string Export(WorkflowDefinition def)
        => JsonSerializer.Serialize(new WorkflowDocument { Definition = def }, new JsonSerializerOptions { WriteIndented = true });

    public static WorkflowDocument Import(string json)
    {
        var doc = JsonSerializer.Deserialize<WorkflowDocument>(json)
                  ?? throw new ArgumentException("Invalid workflow document");
        Validate(doc.Definition);
        return doc;
    }

    public static void Validate(WorkflowDefinition def)
    {
        if (string.IsNullOrWhiteSpace(def.Key))
            throw new ArgumentException("Key required");
        if (def.Steps is null || def.Steps.Count == 0)
            throw new ArgumentException("At least one step required");
        if (def.Steps.Count > 100)
            throw new ArgumentException("Max 100 steps");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in def.Steps)
        {
            if (string.IsNullOrWhiteSpace(s.Id))
                throw new ArgumentException("Step Id required");
            if (!ids.Add(s.Id))
                throw new ArgumentException($"Duplicate step id '{s.Id}'");
            if (string.IsNullOrWhiteSpace(s.Type) || !KnownSteps.Contains(s.Type))
                throw new ArgumentException($"Unknown or missing step type '{s.Type}'");
        }
    }
}
