using NotificationHub.Core.Identity;

namespace NotificationHub.Core.Tests.Identity;

/// <summary>Regression placeholders — expand with Testcontainers/EF InMemory in CI.</summary>
public class MembershipIsolationTests
{
    [Fact]
    public void Permission_names_are_resource_dot_action()
    {
        foreach (var p in IdentityPermissions.All)
            Assert.Contains('.', p);
    }

    [Fact]
    public void PlatformAdmin_role_name_is_stable()
    {
        Assert.Equal("PlatformAdmin", IdentityRoles.PlatformAdmin);
    }

    [Fact]
    public void OrganizationAdmin_cannot_be_confused_with_PlatformAdmin()
    {
        Assert.NotEqual(IdentityRoles.OrganizationAdmin, IdentityRoles.PlatformAdmin);
    }
}
