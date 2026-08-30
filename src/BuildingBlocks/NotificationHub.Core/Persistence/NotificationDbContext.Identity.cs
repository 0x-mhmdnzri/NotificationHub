using Microsoft.EntityFrameworkCore;
using NotificationHub.Core.Identity;

namespace NotificationHub.Core.Persistence;

/// <summary>
/// Identity tables configuration applied from Host/startup without partial DbContext.
/// Call <see cref="ApplyIdentityModel"/> from OnModelCreating after core mappings,
/// or register via modelBuilder in a dedicated startup hook.
/// </summary>
public static class IdentityModelBuilderExtensions
{
    public static void ApplyIdentityModel(this ModelBuilder modelBuilder)
    {
        var org = modelBuilder.Entity<OrganizationEntity>();
        org.ToTable("identity_organizations");
        org.HasKey(x => x.Id);
        org.Property(x => x.Name).HasMaxLength(256).IsRequired();
        org.Property(x => x.Slug).HasMaxLength(128);
        org.Property(x => x.Type).HasMaxLength(64);
        org.Property(x => x.Status).HasMaxLength(32);

        var user = modelBuilder.Entity<IdentityUserEntity>();
        user.ToTable("identity_users");
        user.HasKey(x => x.Id);
        user.Property(x => x.Email).HasMaxLength(320).IsRequired();
        user.Property(x => x.DisplayName).HasMaxLength(256);
        user.Property(x => x.Status).HasMaxLength(32);

        var mem = modelBuilder.Entity<OrganizationMembershipEntity>();
        mem.ToTable("identity_memberships");
        mem.HasKey(x => x.Id);
        mem.Property(x => x.Status).HasMaxLength(32);
        mem.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();

        var role = modelBuilder.Entity<IdentityRoleEntity>();
        role.ToTable("identity_roles");
        role.HasKey(x => x.Id);
        role.Property(x => x.Name).HasMaxLength(128).IsRequired();
        role.HasIndex(x => x.Name).IsUnique();

        var perm = modelBuilder.Entity<IdentityPermissionEntity>();
        perm.ToTable("identity_permissions");
        perm.HasKey(x => x.Id);
        perm.Property(x => x.Name).HasMaxLength(128).IsRequired();
        perm.HasIndex(x => x.Name).IsUnique();

        var rp = modelBuilder.Entity<RolePermissionEntity>();
        rp.ToTable("identity_role_permissions");
        rp.HasKey(x => new { x.RoleId, x.PermissionId });

        var mr = modelBuilder.Entity<MembershipRoleEntity>();
        mr.ToTable("identity_membership_roles");
        mr.HasKey(x => new { x.MembershipId, x.RoleId });

        var inv = modelBuilder.Entity<InvitationEntity>();
        inv.ToTable("identity_invitations");
        inv.HasKey(x => x.Id);
        inv.Property(x => x.Email).HasMaxLength(320).IsRequired();
        inv.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();

        var sess = modelBuilder.Entity<UserSessionEntity>();
        sess.ToTable("identity_sessions");
        sess.HasKey(x => x.Id);
    }
}
