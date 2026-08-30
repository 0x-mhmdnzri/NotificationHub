namespace NotificationHub.Domain.Delivery.ValueObjects;

public readonly record struct NotificationId(Guid Value)
{
    public static NotificationId New() => new(Guid.NewGuid());
    public static NotificationId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("NotificationId cannot be empty.");
        return new NotificationId(value);
    }
    public override string ToString() => Value.ToString();
}
