using Microsoft.EntityFrameworkCore;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Persistence;
using NotificationHub.Core.Preferences;

namespace NotificationHub.Core.Compliance;

public interface IComplianceService
{
    Task<ComplianceExport> ExportUserAsync(string userId, string? tenantId = null, CancellationToken ct = default);
    Task DeleteUserAsync(string userId, string? tenantId = null, CancellationToken ct = default);
}

public sealed class ComplianceService : IComplianceService
{
    private readonly NotificationDbContext _db;
    private readonly IPreferenceService _preferences;
    private readonly IConsentService _consents;

    public ComplianceService(NotificationDbContext db, IPreferenceService preferences, IConsentService consents)
    {
        _db = db;
        _preferences = preferences;
        _consents = consents;
    }

    public async Task<ComplianceExport> ExportUserAsync(string userId, string? tenantId = null, CancellationToken ct = default)
    {
        var pref = await _preferences.GetAsync(userId, tenantId, ct);
        var consentList = await _consents.ListAsync(userId, tenantId, ct);

        var notifQuery = _db.NotificationStatuses.AsNoTracking().Where(x => x.Recipient == userId);
        var inAppQuery = _db.InAppMessages.AsNoTracking().Where(x => x.UserId == userId);
        if (!string.IsNullOrEmpty(tenantId))
        {
            notifQuery = notifQuery.Where(x => x.TenantId == tenantId);
            inAppQuery = inAppQuery.Where(x => x.TenantId == tenantId);
        }

        var notifications = await notifQuery.OrderByDescending(x => x.CreatedAt).Take(1000).ToListAsync(ct);
        var inApps = await inAppQuery.OrderByDescending(x => x.CreatedAt).Take(1000).ToListAsync(ct);

        return new ComplianceExport
        {
            UserId = userId,
            TenantId = tenantId,
            Preference = pref,
            Consents = consentList,
            Notifications = notifications.Select(x => x.ToModel()).ToList(),
            InAppMessages = inApps.Select(x => new InAppMessage
            {
                Id = x.Id, UserId = x.UserId, TenantId = x.TenantId, Title = x.Title, Body = x.Body,
                IsRead = x.IsRead, CreatedAt = x.CreatedAt
            }).ToList()
        };
    }

    public async Task DeleteUserAsync(string userId, string? tenantId = null, CancellationToken ct = default)
    {
        var prefs = _db.UserPreferences.Where(x => x.UserId == userId);
        var notifs = _db.NotificationStatuses.Where(x => x.Recipient == userId);
        var inApps = _db.InAppMessages.Where(x => x.UserId == userId);
        var consents = _db.ConsentLedger.Where(x => x.SubjectId == userId);
        if (!string.IsNullOrEmpty(tenantId))
        {
            prefs = prefs.Where(x => x.TenantId == tenantId);
            notifs = notifs.Where(x => x.TenantId == tenantId);
            inApps = inApps.Where(x => x.TenantId == tenantId);
            consents = consents.Where(x => x.TenantId == tenantId);
        }
        _db.UserPreferences.RemoveRange(await prefs.ToListAsync(ct));
        _db.NotificationStatuses.RemoveRange(await notifs.ToListAsync(ct));
        _db.InAppMessages.RemoveRange(await inApps.ToListAsync(ct));
        // GDPR erase: remove consent rows for subject as part of full delete
        _db.ConsentLedger.RemoveRange(await consents.ToListAsync(ct));
        await _db.SaveChangesAsync(ct);
    }
}
