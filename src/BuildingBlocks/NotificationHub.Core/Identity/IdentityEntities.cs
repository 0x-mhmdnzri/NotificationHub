namespace NotificationHub.Core.Identity;

/// <summary>B2B customer company — isolation unit (Tenant).</summary>
public sealed class OrganizationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string? Slug { get; set; }
    /// <summary>Merchant | Enterprise | Partner | Platform | …</summary>
    public string Type { get; set; } = "Merchant";
    /// <summary>Active | Suspended | Closed</summary>
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>Human identity. Email is an attribute, not the immutable key.</summary>
public sealed class IdentityUserEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string? DisplayName { get; set; }
    public string? Phone { get; set; }
    /// <summary>Active | Locked | Suspended | Disabled | Deleted</summary>
    public string Status { get; set; } = "Active";
    public string? PasswordHash { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

/// <summary>User ↔ Organization membership with lifecycle.</summary>
public sealed class OrganizationMembershipEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public Guid UserId { get; set; }
    /// <summary>Invited | Pending | Active | Suspended | Revoked</summary>
    public string Status { get; set; } = "Invited";
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; set; }
}

public sealed class IdentityRoleEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    /// <summary>Platform | Organization</summary>
    public string Scope { get; set; } = "Organization";
    public string? Description { get; set; }
    public bool IsSystem { get; set; }
}

public sealed class IdentityPermissionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>resource.action e.g. notification.send</summary>
    public string Name { get; set; } = "";
    public string? Description { get; set; }
}

public sealed class RolePermissionEntity
{
    public Guid RoleId { get; set; }
    public Guid PermissionId { get; set; }
}

public sealed class MembershipRoleEntity
{
    public Guid MembershipId { get; set; }
    public Guid RoleId { get; set; }
}

public sealed class InvitationEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrganizationId { get; set; }
    public string Email { get; set; } = "";
    public string TokenHash { get; set; } = "";
    public Guid? InvitedByUserId { get; set; }
    public string? RoleName { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? AcceptedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class UserSessionEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid? OrganizationId { get; set; }
    public string? ClientId { get; set; }
    public string? JwtId { get; set; }
    public string? RefreshTokenHash { get; set; }
    public string? Ip { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public bool IsActive { get; set; } = true;
}
