using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Core.Identity;

/// <summary>Ensures identity tables + seeds system roles/permissions (Phase-style, no breaking API keys).</summary>
public static class IdentitySchema
{
    public static async Task EnsureAsync(NotificationDbContext db, ILogger? log = null, CancellationToken ct = default)
    {
        await db.Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS identity_organizations (
                "Id" uuid PRIMARY KEY,
                "Name" varchar(256) NOT NULL,
                "Slug" varchar(128),
                "Type" varchar(64) NOT NULL DEFAULT 'Merchant',
                "Status" varchar(32) NOT NULL DEFAULT 'Active',
                "CreatedAt" timestamptz NOT NULL,
                "UpdatedAt" timestamptz NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_identity_org_slug ON identity_organizations ("Slug") WHERE "Slug" IS NOT NULL;

            CREATE TABLE IF NOT EXISTS identity_users (
                "Id" uuid PRIMARY KEY,
                "Email" varchar(320) NOT NULL,
                "DisplayName" varchar(256),
                "Phone" varchar(64),
                "Status" varchar(32) NOT NULL DEFAULT 'Active',
                "PasswordHash" varchar(512),
                "CreatedAt" timestamptz NOT NULL,
                "UpdatedAt" timestamptz NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_identity_users_email ON identity_users (lower("Email"));

            CREATE TABLE IF NOT EXISTS identity_memberships (
                "Id" uuid PRIMARY KEY,
                "OrganizationId" uuid NOT NULL REFERENCES identity_organizations("Id"),
                "UserId" uuid NOT NULL REFERENCES identity_users("Id"),
                "Status" varchar(32) NOT NULL DEFAULT 'Invited',
                "JoinedAt" timestamptz NOT NULL,
                "RevokedAt" timestamptz
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_identity_membership_org_user
                ON identity_memberships ("OrganizationId", "UserId");
            CREATE INDEX IF NOT EXISTS ix_identity_membership_user ON identity_memberships ("UserId");

            CREATE TABLE IF NOT EXISTS identity_roles (
                "Id" uuid PRIMARY KEY,
                "Name" varchar(128) NOT NULL,
                "Scope" varchar(32) NOT NULL DEFAULT 'Organization',
                "Description" varchar(512),
                "IsSystem" boolean NOT NULL DEFAULT false
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_identity_roles_name ON identity_roles ("Name");

            CREATE TABLE IF NOT EXISTS identity_permissions (
                "Id" uuid PRIMARY KEY,
                "Name" varchar(128) NOT NULL,
                "Description" varchar(512)
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_identity_permissions_name ON identity_permissions ("Name");

            CREATE TABLE IF NOT EXISTS identity_role_permissions (
                "RoleId" uuid NOT NULL REFERENCES identity_roles("Id"),
                "PermissionId" uuid NOT NULL REFERENCES identity_permissions("Id"),
                PRIMARY KEY ("RoleId", "PermissionId")
            );

            CREATE TABLE IF NOT EXISTS identity_membership_roles (
                "MembershipId" uuid NOT NULL REFERENCES identity_memberships("Id"),
                "RoleId" uuid NOT NULL REFERENCES identity_roles("Id"),
                PRIMARY KEY ("MembershipId", "RoleId")
            );

            CREATE TABLE IF NOT EXISTS identity_invitations (
                "Id" uuid PRIMARY KEY,
                "OrganizationId" uuid NOT NULL REFERENCES identity_organizations("Id"),
                "Email" varchar(320) NOT NULL,
                "TokenHash" varchar(128) NOT NULL,
                "InvitedByUserId" uuid,
                "RoleName" varchar(128),
                "ExpiresAt" timestamptz NOT NULL,
                "AcceptedAt" timestamptz,
                "CreatedAt" timestamptz NOT NULL
            );
            CREATE UNIQUE INDEX IF NOT EXISTS ix_identity_invitations_token ON identity_invitations ("TokenHash");

            CREATE TABLE IF NOT EXISTS identity_sessions (
                "Id" uuid PRIMARY KEY,
                "UserId" uuid NOT NULL REFERENCES identity_users("Id"),
                "OrganizationId" uuid,
                "ClientId" varchar(128),
                "JwtId" varchar(128),
                "RefreshTokenHash" varchar(128),
                "Ip" varchar(64),
                "UserAgent" varchar(512),
                "CreatedAt" timestamptz NOT NULL,
                "LastSeenAt" timestamptz NOT NULL,
                "ExpiresAt" timestamptz NOT NULL,
                "RevokedAt" timestamptz,
                "IsActive" boolean NOT NULL DEFAULT true
            );
            CREATE INDEX IF NOT EXISTS ix_identity_sessions_user ON identity_sessions ("UserId") WHERE "IsActive" = true;
            """,
            ct);

        await SeedAsync(db, log, ct);
        log?.LogInformation("Identity schema ensured");
    }

    static async Task SeedAsync(NotificationDbContext db, ILogger? log, CancellationToken ct)
    {
        foreach (var name in IdentityPermissions.All)
        {
            if (await db.Set<IdentityPermissionEntity>().AnyAsync(p => p.Name == name, ct))
                continue;
            db.Set<IdentityPermissionEntity>().Add(new IdentityPermissionEntity { Name = name });
        }

        await db.SaveChangesAsync(ct);

        await EnsureRoleAsync(db, IdentityRoles.PlatformAdmin, "Platform", true, IdentityPermissions.All, ct);
        await EnsureRoleAsync(db, IdentityRoles.OrganizationOwner, "Organization", true, IdentityPermissions.All, ct);
        await EnsureRoleAsync(
            db,
            IdentityRoles.OrganizationAdmin,
            "Organization",
            true,
            [
                IdentityPermissions.NotificationRead,
                IdentityPermissions.NotificationSend,
                IdentityPermissions.TemplateRead,
                IdentityPermissions.TemplateWrite,
                IdentityPermissions.TemplateDelete,
                IdentityPermissions.CampaignRead,
                IdentityPermissions.CampaignCreate,
                IdentityPermissions.CampaignStart,
                IdentityPermissions.CampaignCancel,
                IdentityPermissions.MemberInvite,
                IdentityPermissions.MemberRoleAssign,
                IdentityPermissions.MemberSuspend,
                IdentityPermissions.MemberRead,
                IdentityPermissions.OrganizationRead,
                IdentityPermissions.OrganizationUpdate,
                IdentityPermissions.AuditRead
            ],
            ct);
        await EnsureRoleAsync(
            db,
            IdentityRoles.NotificationOperator,
            "Organization",
            true,
            [
                IdentityPermissions.NotificationRead,
                IdentityPermissions.NotificationSend,
                IdentityPermissions.TemplateRead,
                IdentityPermissions.TemplateWrite,
                IdentityPermissions.CampaignRead,
                IdentityPermissions.CampaignCreate,
                IdentityPermissions.CampaignStart
            ],
            ct);
        await EnsureRoleAsync(
            db,
            IdentityRoles.Viewer,
            "Organization",
            true,
            [
                IdentityPermissions.NotificationRead,
                IdentityPermissions.TemplateRead,
                IdentityPermissions.CampaignRead,
                IdentityPermissions.MemberRead,
                IdentityPermissions.OrganizationRead
            ],
            ct);
        await EnsureRoleAsync(
            db,
            IdentityRoles.Auditor,
            "Organization",
            true,
            [
                IdentityPermissions.NotificationRead,
                IdentityPermissions.TemplateRead,
                IdentityPermissions.CampaignRead,
                IdentityPermissions.AuditRead,
                IdentityPermissions.OrganizationRead
            ],
            ct);

        log?.LogInformation("Identity roles/permissions seeded");
    }

    static async Task EnsureRoleAsync(
        NotificationDbContext db,
        string name,
        string scope,
        bool system,
        IReadOnlyList<string> permissionNames,
        CancellationToken ct)
    {
        var role = await db.Set<IdentityRoleEntity>().FirstOrDefaultAsync(r => r.Name == name, ct);
        if (role is null)
        {
            role = new IdentityRoleEntity { Name = name, Scope = scope, IsSystem = system };
            db.Set<IdentityRoleEntity>().Add(role);
            await db.SaveChangesAsync(ct);
        }

        var perms = await db.Set<IdentityPermissionEntity>()
            .Where(p => permissionNames.Contains(p.Name))
            .ToListAsync(ct);
        foreach (var p in perms)
        {
            if (await db.Set<RolePermissionEntity>().AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == p.Id, ct))
                continue;
            db.Set<RolePermissionEntity>().Add(new RolePermissionEntity { RoleId = role.Id, PermissionId = p.Id });
        }

        await db.SaveChangesAsync(ct);
    }
}
