namespace NotificationHub.Core.I18n;

public interface ILocalizationCatalog
{
    Task SetAsync(string key, string locale, string value, string? tenantId = null, CancellationToken ct = default);
    Task<string?> GetAsync(string key, string locale, string? tenantId = null, CancellationToken ct = default);
    Task<IReadOnlyDictionary<string, string>> GetAllAsync(string locale, string? tenantId = null, CancellationToken ct = default);
}
