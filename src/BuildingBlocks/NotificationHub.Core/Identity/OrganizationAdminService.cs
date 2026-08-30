using Microsoft.EntityFrameworkCore;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Identity;

public sealed class OrganizationAdminService(NotificationDbContext db) : IOrganizationAdminService
{
    public async Task<OrganizationDto?> GetAsync(Guid organizationId, CancellationToken ct = default)
    {
        var o = await db.Set<OrganizationEntity>().AsNoTracking().FirstOrDefaultAsync(x => x.Id == organizationId, ct);
        return o is null ? null : new OrganizationDto(o.Id, o.Name, o.Slug, o.Type, o.Status);
    }

    public async Task<OrganizationDto> CreateAsync(string name, string? slug, string type, CancellationToken ct = default)
    {
        var o = new OrganizationEntity
        {
            Name = name.Trim(),
            Slug = string.IsNullOrWhiteSpace(slug) ? null : slug.Trim().ToLowerInvariant(),
            Type = string.IsNullOrWhiteSpace(type) ? "Merchant" : type,
            Status = "Active"
        };
        db.Set<OrganizationEntity>().Add(o);
        await db.SaveChangesAsync(ct);
        return new OrganizationDto(o.Id, o.Name, o.Slug, o.Type, o.Status);
    }

    public async Task<OrganizationDto?> UpdateAsync(Guid organizationId, string? name, string? status, CancellationToken ct = default)
    {
        var o = await db.Set<OrganizationEntity>().FirstOrDefaultAsync(x => x.Id == organizationId, ct);
        if (o is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            o.Name = name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            o.Status = status;
        }

        o.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return new OrganizationDto(o.Id, o.Name, o.Slug, o.Type, o.Status);
    }

    public async Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid organizationId, CancellationToken ct = default)
    {
        var rows = await (
            from m in db.Set<OrganizationMembershipEntity>().AsNoTracking()
            join u in db.Set<IdentityUserEntity>().AsNoTracking() on m.UserId equals u.Id
            where m.OrganizationId == organizationId && m.Status != "Revoked"
            select new { m, u }).ToListAsync(ct);

        var list = new List<MemberDto>();
        foreach (var row in rows)
        {
            var roleIds = await db.Set<MembershipRoleEntity>().AsNoTracking()
                .Where(mr => mr.MembershipId == row.m.Id)
                .Select(mr => mr.RoleId)
                .ToListAsync(ct);
            var roles = await db.Set<IdentityRoleEntity>().AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .Select(r => r.Name)
                .ToListAsync(ct);
            list.Add(new MemberDto(row.m.Id, row.u.Id, row.u.Email, row.u.DisplayName, row.m.Status, roles));
        }

        return list;
    }

    public async Task<bool> AssignRoleAsync(Guid membershipId, string roleName, CancellationToken ct = default)
    {
        var mem = await db.Set<OrganizationMembershipEntity>().FirstOrDefaultAsync(m => m.Id == membershipId, ct);
        if (mem is null)
        {
            return false;
        }

        var role = await db.Set<IdentityRoleEntity>().FirstOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is null)
        {
            return false;
        }

        if (string.Equals(role.Scope, "Platform", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (await db.Set<MembershipRoleEntity>().AnyAsync(mr => mr.MembershipId == membershipId && mr.RoleId == role.Id, ct))
        {
            return true;
        }

        db.Set<MembershipRoleEntity>().Add(new MembershipRoleEntity { MembershipId = membershipId, RoleId = role.Id });
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> RemoveRoleAsync(Guid membershipId, string roleName, CancellationToken ct = default)
    {
        var role = await db.Set<IdentityRoleEntity>().FirstOrDefaultAsync(r => r.Name == roleName, ct);
        if (role is null)
        {
            return false;
        }

        var link = await db.Set<MembershipRoleEntity>()
            .FirstOrDefaultAsync(mr => mr.MembershipId == membershipId && mr.RoleId == role.Id, ct);
        if (link is null)
        {
            return true;
        }

        db.Set<MembershipRoleEntity>().Remove(link);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SetMembershipStatusAsync(Guid membershipId, string status, CancellationToken ct = default)
    {
        var allowed = new[] { "Active", "Suspended", "Revoked" };
        if (!allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
        {
            return false;
        }

        var mem = await db.Set<OrganizationMembershipEntity>().FirstOrDefaultAsync(m => m.Id == membershipId, ct);
        if (mem is null)
        {
            return false;
        }

        mem.Status = status;
        if (string.Equals(status, "Revoked", StringComparison.OrdinalIgnoreCase))
        {
            mem.RevokedAt = DateTimeOffset.UtcNow;
        }
        else if (string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            mem.RevokedAt = null;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }
}
