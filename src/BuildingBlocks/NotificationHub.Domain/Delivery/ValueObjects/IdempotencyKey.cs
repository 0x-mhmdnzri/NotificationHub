using NotificationHub.Domain.Common;

namespace NotificationHub.Domain.Delivery.ValueObjects;

public sealed record IdempotencyKey
{
    public string Value { get; }

    private IdempotencyKey(string value) => Value = value;

    public static IdempotencyKey? From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var v = value.Trim();
        if (v.Length > 256)
            throw new DomainException("Idempotency key exceeds maximum length.");
        return new IdempotencyKey(v);
    }

    public override string ToString() => Value;
}
