using NotificationHub.Domain.Common;

namespace NotificationHub.Domain.Delivery.ValueObjects;

public sealed record TemplateKey
{
    public string Value { get; }

    private TemplateKey(string value) => Value = value;

    public static TemplateKey Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Template key cannot be empty.");
        return new TemplateKey(value.Trim());
    }

    public override string ToString() => Value;
}
