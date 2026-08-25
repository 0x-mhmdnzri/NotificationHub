namespace NotificationHub.Core.Expressions;

/// <summary>Evaluates workflow condition expressions against a data bag (SRP).</summary>
public interface IExpressionEvaluator
{
    bool Evaluate(string? expression, IReadOnlyDictionary<string, object?> data);
}
