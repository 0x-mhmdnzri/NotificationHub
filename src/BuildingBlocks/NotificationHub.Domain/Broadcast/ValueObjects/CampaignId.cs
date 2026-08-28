namespace NotificationHub.Domain.Broadcast.ValueObjects;

public readonly record struct CampaignId(Guid Value)
{
    public static CampaignId New() => new(Guid.NewGuid());
    public static CampaignId From(Guid value)
    {
        if (value == Guid.Empty) throw new ArgumentException("CampaignId cannot be empty.");
        return new CampaignId(value);
    }
    public override string ToString() => Value.ToString();
}
