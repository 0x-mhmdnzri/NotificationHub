using NotificationHub.Domain.Common;

namespace NotificationHub.Domain.Preferences.ValueObjects;

public sealed record UserId
{
    public string Value { get; }
    private UserId(string value) => Value = value;
    public static UserId Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("UserId cannot be empty.");
        return new UserId(value.Trim());
    }
    public override string ToString() => Value;
}
