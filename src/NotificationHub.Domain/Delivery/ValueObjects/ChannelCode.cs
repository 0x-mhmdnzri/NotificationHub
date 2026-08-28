using NotificationHub.Domain.Common;

namespace NotificationHub.Domain.Delivery.ValueObjects;

public sealed record ChannelCode
{
    public string Value { get; }

    private ChannelCode(string value) => Value = value;

    public static ChannelCode Create(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new DomainException("Channel cannot be empty.");
        return new ChannelCode(value.Trim().ToLowerInvariant());
    }

    public override string ToString() => Value;
}
