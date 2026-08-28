using NotificationHub.Domain.Common;

namespace NotificationHub.Domain.Delivery.ValueObjects;

/// <summary>Channel-agnostic recipient address (email, phone, device token, chat id).</summary>
public sealed record RecipientAddress
{
    public string Value { get; }

    private RecipientAddress(string value) => Value = value;

    public static RecipientAddress Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Recipient address cannot be empty.");
        var trimmed = value.Trim();
        if (trimmed.Length > 512)
            throw new DomainException("Recipient address exceeds maximum length.");
        return new RecipientAddress(trimmed);
    }

    public override string ToString() => Value;
}
