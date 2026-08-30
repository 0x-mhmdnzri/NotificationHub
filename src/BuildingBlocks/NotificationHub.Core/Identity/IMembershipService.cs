namespace NotificationHub.Core.Identity;

public interface IMembershipService
{
    Task<MembershipSnapshot?> GetActiveMembershipAsync(Guid userId, Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationMembershipDto>> ListMembershipsAsync(Guid userId, CancellationToken ct = default);
    Task<AuthMeDto?> GetMeAsync(Guid userId, Guid? organizationId, CancellationToken ct = default);
    Task<InviteResult> InviteAsync(Guid organizationId, string email, string? roleName, Guid invitedByUserId, CancellationToken ct = default);
    Task<bool> AcceptInviteAsync(string rawToken, Guid userId, CancellationToken ct = default);
    Task RevokeSessionAsync(Guid userId, Guid? sessionId, string? jwtId, CancellationToken ct = default);
    Task RecordSecurityEventAsync(string action, Guid? userId, Guid? organizationId, string? details, CancellationToken ct = default);
}

public sealed record MembershipSnapshot(
    Guid MembershipId,
    Guid OrganizationId,
    Guid UserId,
    string Status,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record OrganizationMembershipDto(
    Guid MembershipId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationStatus,
    string MembershipStatus,
    IReadOnlyList<string> Roles);

public sealed record AuthMeDto(
    Guid UserId,
    string Email,
    string? DisplayName,
    Guid? OrganizationId,
    string? OrganizationName,
    Guid? MembershipId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public sealed record InviteResult(bool Success, Guid? InvitationId, string? Error);
