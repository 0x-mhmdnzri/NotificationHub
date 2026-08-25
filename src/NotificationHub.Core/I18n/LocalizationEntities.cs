namespace NotificationHub.Core.I18n;

public sealed class LocalizationEntryEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Key { get; set; } = "";
    public string Locale { get; set; } = "en";
    public string? TenantId { get; set; }
    public string Value { get; set; } = "";
}
