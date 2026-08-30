namespace NotificationHub.Core.Identity;

/// <summary>Active organization context for the current request (human JWT path).</summary>
public interface ITenantContext
{
    Guid? OrganizationId { get; }
    Guid? UserId { get; }
    Guid? MembershipId { get; }
    bool HasOrganization => OrganizationId is not null;
}

public sealed class NullTenantContext : ITenantContext
{
    public Guid? OrganizationId => null;
    public Guid? UserId => null;
    public Guid? MembershipId => null;
}
