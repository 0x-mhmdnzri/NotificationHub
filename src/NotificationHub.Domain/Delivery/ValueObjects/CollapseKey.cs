using NotificationHub.Domain.Common;

namespace NotificationHub.Domain.Delivery.ValueObjects;

public sealed record CollapseKey
{
    public string Value { get; }

    private CollapseKey(string value) => Value = value;

    public static CollapseKey? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return new CollapseKey(value.Trim());
    }

    public override string ToString() => Value;
}
