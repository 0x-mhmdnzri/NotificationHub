namespace NotificationHub.Core.Identity;

public interface IOrganizationAdminService
{
    Task<OrganizationDto?> GetAsync(Guid organizationId, CancellationToken ct = default);
    Task<OrganizationDto> CreateAsync(string name, string? slug, string type, CancellationToken ct = default);
    Task<OrganizationDto?> UpdateAsync(Guid organizationId, string? name, string? status, CancellationToken ct = default);
    Task<IReadOnlyList<MemberDto>> ListMembersAsync(Guid organizationId, CancellationToken ct = default);
    Task<bool> AssignRoleAsync(Guid membershipId, string roleName, CancellationToken ct = default);
    Task<bool> RemoveRoleAsync(Guid membershipId, string roleName, CancellationToken ct = default);
    Task<bool> SetMembershipStatusAsync(Guid membershipId, string status, CancellationToken ct = default);
}

public sealed record OrganizationDto(Guid Id, string Name, string? Slug, string Type, string Status);
public sealed record MemberDto(Guid MembershipId, Guid UserId, string Email, string? DisplayName, string Status, IReadOnlyList<string> Roles);
