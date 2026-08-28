namespace NotificationHub.Domain.Preferences;

public readonly record struct PreferenceId(Guid Value)
{
    public static PreferenceId New() => new(Guid.NewGuid());
    public static PreferenceId From(Guid value) => new(value == Guid.Empty ? throw new ArgumentException("empty") : value);
}
