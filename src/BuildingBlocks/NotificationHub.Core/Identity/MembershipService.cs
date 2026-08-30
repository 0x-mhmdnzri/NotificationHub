using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Identity;

public sealed class MembershipService(NotificationDbContext db, ILogger<MembershipService> log) : IMembershipService
{
    public async Task<MembershipSnapshot?> GetActiveMembershipAsync(Guid userId, Guid organizationId, CancellationToken ct = default)
    {
        var mem = await db.OrganizationMemberships.AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId, ct);
        if (mem is null || !string.Equals(mem.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return null;

        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org is null || !string.Equals(org.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return null;

        var roleIds = await db.MembershipRoles.AsNoTracking()
            .Where(mr => mr.MembershipId == mem.Id)
            .Select(mr => mr.RoleId)
            .ToListAsync(ct);

        var roles = await db.IdentityRoles.AsNoTracking()
            .Where(r => roleIds.Contains(r.Id))
            .Select(r => r.Name)
            .ToListAsync(ct);

        var permIds = await db.RolePermissions.AsNoTracking()
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToListAsync(ct);

        var permissions = await db.IdentityPermissions.AsNoTracking()
            .Where(p => permIds.Contains(p.Id))
            .Select(p => p.Name)
            .ToListAsync(ct);

        return new MembershipSnapshot(mem.Id, organizationId, userId, mem.Status, roles, permissions);
    }

    public async Task<IReadOnlyList<OrganizationMembershipDto>> ListMembershipsAsync(Guid userId, CancellationToken ct = default)
    {
        var q =
            from m in db.OrganizationMemberships.AsNoTracking()
            join o in db.Organizations.AsNoTracking() on m.OrganizationId equals o.Id
            where m.UserId == userId && m.Status != "Revoked"
            select new { m, o };

        var rows = await q.ToListAsync(ct);
        var result = new List<OrganizationMembershipDto>();
        foreach (var row in rows)
        {
            var roleIds = await db.MembershipRoles.AsNoTracking()
                .Where(mr => mr.MembershipId == row.m.Id).Select(mr => mr.RoleId).ToListAsync(ct);
            var roles = await db.IdentityRoles.AsNoTracking()
                .Where(r => roleIds.Contains(r.Id)).Select(r => r.Name).ToListAsync(ct);
            result.Add(new OrganizationMembershipDto(
                row.m.Id, row.o.Id, row.o.Name, row.o.Status, row.m.Status, roles));
        }
        return result;
    }

    public async Task<AuthMeDto?> GetMeAsync(Guid userId, Guid? organizationId, CancellationToken ct = default)
    {
        var user = await db.IdentityUsers.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || string.Equals(user.Status, "Disabled", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Status, "Deleted", StringComparison.OrdinalIgnoreCase))
            return null;

        if (organizationId is null)
        {
            return new AuthMeDto(user.Id, user.Email, user.DisplayName, null, null, null, [], []);
        }

        var snap = await GetActiveMembershipAsync(userId, organizationId.Value, ct);
        if (snap is null)
            return new AuthMeDto(user.Id, user.Email, user.DisplayName, null, null, null, [], []);

        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.Id == organizationId.Value, ct);
        return new AuthMeDto(
            user.Id, user.Email, user.DisplayName,
            org.Id, org.Name, snap.MembershipId,
            snap.Roles, snap.Permissions);
    }

    public async Task<InviteResult> InviteAsync(
        Guid organizationId, string email, string? roleName, Guid invitedByUserId, CancellationToken ct = default)
    {
        email = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email))
            return new InviteResult(false, null, "invalid_email");

        var org = await db.Organizations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == organizationId, ct);
        if (org is null || !string.Equals(org.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return new InviteResult(false, null, "organization_inactive");

        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var hash = HashToken(raw);
        var inv = new InvitationEntity
        {
            OrganizationId = organizationId,
            Email = email,
            TokenHash = hash,
            InvitedByUserId = invitedByUserId,
            RoleName = roleName ?? IdentityRoles.Viewer,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
        };
        db.Invitations.Add(inv);
        await db.SaveChangesAsync(ct);

        // Raw token returned only once via out-of-band channel in production; stored hashed.
        log.LogInformation("Invitation created {InvitationId} for org {OrgId}", inv.Id, organizationId);
        await RecordSecurityEventAsync("UserInvited", invitedByUserId, organizationId, email, ct);

        // For API response in Sprint 2 we return id only (token delivery via email later).
        return new InviteResult(true, inv.Id, null);
    }

    public async Task<bool> AcceptInviteAsync(string rawToken, Guid userId, CancellationToken ct = default)
    {
        var hash = HashToken(rawToken);
        var inv = await db.Invitations.FirstOrDefaultAsync(i => i.TokenHash == hash, ct);
        if (inv is null || inv.AcceptedAt is not null || inv.ExpiresAt < DateTimeOffset.UtcNow)
            return false;

        var user = await db.IdentityUsers.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null)
            return false;

        if (!string.Equals(user.Email, inv.Email, StringComparison.OrdinalIgnoreCase))
            return false;

        var existing = await db.OrganizationMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == inv.OrganizationId, ct);

        OrganizationMembershipEntity mem;
        if (existing is null)
        {
            mem = new OrganizationMembershipEntity
            {
                OrganizationId = inv.OrganizationId,
                UserId = userId,
                Status = "Active",
                JoinedAt = DateTimeOffset.UtcNow
            };
            db.OrganizationMemberships.Add(mem);
            await db.SaveChangesAsync(ct);
        }
        else
        {
            mem = existing;
            mem.Status = "Active";
            mem.RevokedAt = null;
        }

        if (!string.IsNullOrWhiteSpace(inv.RoleName))
        {
            var role = await db.IdentityRoles.FirstOrDefaultAsync(r => r.Name == inv.RoleName, ct);
            if (role is not null &&
                !await db.MembershipRoles.AnyAsync(mr => mr.MembershipId == mem.Id && mr.RoleId == role.Id, ct))
            {
                db.MembershipRoles.Add(new MembershipRoleEntity { MembershipId = mem.Id, RoleId = role.Id });
            }
        }

        inv.AcceptedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await RecordSecurityEventAsync("UserActivated", userId, inv.OrganizationId, null, ct);
        return true;
    }

    public async Task RevokeSessionAsync(Guid userId, Guid? sessionId, string? jwtId, CancellationToken ct = default)
    {
        IQueryable<UserSessionEntity> q = db.UserSessions.Where(s => s.UserId == userId && s.IsActive);
        if (sessionId is not null)
            q = q.Where(s => s.Id == sessionId);
        else if (!string.IsNullOrEmpty(jwtId))
            q = q.Where(s => s.JwtId == jwtId);

        var sessions = await q.ToListAsync(ct);
        foreach (var s in sessions)
        {
            s.IsActive = false;
            s.RevokedAt = DateTimeOffset.UtcNow;
        }
        await db.SaveChangesAsync(ct);
        await RecordSecurityEventAsync("SessionRevoked", userId, null, sessionId?.ToString() ?? jwtId, ct);
    }

    public async Task RecordSecurityEventAsync(
        string action, Guid? userId, Guid? organizationId, string? details, CancellationToken ct = default)
    {
        db.AuditEntries.Add(new AuditEntryEntity
        {
            Action = action,
            TenantId = organizationId?.ToString(),
            Actor = userId?.ToString(),
            Details = details is { Length: > 500 } ? details[..500] : details,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);
    }

    static string HashToken(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
