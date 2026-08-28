namespace NotificationHub.Domain.Common;

public readonly record struct TenantId(string Value)
{
    public static TenantId? From(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : new TenantId(value.Trim());

    public override string ToString() => Value;
}
