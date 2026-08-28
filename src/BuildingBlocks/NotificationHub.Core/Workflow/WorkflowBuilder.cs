using System.Text.Json;
using NotificationHub.Abstractions.Models;

namespace NotificationHub.Core.Workflow;

/// <summary>F24 — code-first fluent workflow that compiles to WorkflowDefinition.</summary>
public sealed class WorkflowBuilder
{
    private readonly string _key;
    private string? _tenantId;
    private readonly List<WorkflowStep> _steps = new();
    private int _seq;

    private WorkflowBuilder(string key) => _key = key;

    public static WorkflowBuilder Create(string key) => new(key);

    public WorkflowBuilder ForTenant(string? tenantId) { _tenantId = tenantId; return this; }

    public WorkflowBuilder Send(string stepId, string channel, string templateKey, string? next = null)
    {
        _steps.Add(new WorkflowStep { Id = stepId, Type = "send", Channel = channel, TemplateKey = templateKey, Next = next });
        return this;
    }

    public WorkflowBuilder Delay(string stepId, int seconds, string? next = null)
    {
        _steps.Add(new WorkflowStep { Id = stepId, Type = "delay", DelaySeconds = seconds, Next = next });
        return this;
    }

    public WorkflowBuilder Condition(string stepId, string expression, string onTrue, string onFalse)
    {
        _steps.Add(new WorkflowStep
        {
            Id = stepId, Type = "condition", ConditionExpression = expression,
            NextOnTrue = onTrue, NextOnFalse = onFalse
        });
        return this;
    }

    public WorkflowBuilder Http(string stepId, string url, string method = "GET", string? next = null, object? headers = null)
    {
        var cfg = JsonSerializer.Serialize(new { url, method, next, headers });
        _steps.Add(new WorkflowStep { Id = stepId, Type = "http", Next = next, ConfigJson = cfg });
        return this;
    }

    /// <summary>Auto-id helper: send-1, delay-2, ...</summary>
    public WorkflowBuilder SendEmail(string templateKey, string? next = null)
        => Send($"s{++_seq}", "email", templateKey, next);

    public WorkflowDefinition Build(bool isActive = true)
    {
        if (string.IsNullOrWhiteSpace(_key)) throw new ArgumentException("Key required");
        if (_steps.Count == 0) throw new ArgumentException("At least one step required");
        var def = new WorkflowDefinition
        {
            Key = _key,
            TenantId = _tenantId,
            IsActive = isActive,
            Steps = _steps.ToList()
        };
        WorkflowDsl.Validate(def);
        return def;
    }
}
