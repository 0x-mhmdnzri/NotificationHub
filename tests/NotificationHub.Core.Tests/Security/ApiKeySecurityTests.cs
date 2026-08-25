using FluentAssertions;
using NotificationHub.Abstractions.Models;
using NotificationHub.Core.Security;
using NotificationHub.Core.Tests.Helpers;

namespace NotificationHub.Core.Tests.Security;

public class ApiKeySecurityTests
{
    [Fact]
    public async Task TC_SEC_001_CreateAndValidate_ApiKey()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresApiKeyStore(db);
        var validator = new ApiKeyValidator(store);
        var plain = ApiKeyHasher.GeneratePlainKey();
        var hash = ApiKeyHasher.Hash(plain);

        var created = await store.CreateAsync(new CreateApiKeyRequest
        {
            Name = "tenant-a-sender",
            TenantId = "tenant-a",
            Roles = [AppRoles.Sender, AppRoles.Reader]
        }, plain, hash);

        created.PlainKey.Should().Be(plain);
        created.TenantId.Should().Be("tenant-a");

        var auth = await validator.ValidateAsync(plain);
        auth.Should().NotBeNull();
        auth!.TenantId.Should().Be("tenant-a");
        auth.HasRole(AppRoles.Sender).Should().BeTrue();
        auth.IsAdmin.Should().BeFalse();
    }

    [Fact]
    public async Task TC_SEC_002_InvalidKey_ReturnsNull()
    {
        await using var db = TestFixtures.CreateDbContext();
        var validator = new ApiKeyValidator(new PostgresApiKeyStore(db));
        var auth = await validator.ValidateAsync("nh_does_not_exist");
        auth.Should().BeNull();
    }

    [Fact]
    public async Task TC_SEC_003_RevokedKey_FailsValidation()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresApiKeyStore(db);
        var validator = new ApiKeyValidator(store);
        var plain = ApiKeyHasher.GeneratePlainKey();
        var created = await store.CreateAsync(new CreateApiKeyRequest
        {
            Name = "tmp", Roles = [AppRoles.Reader]
        }, plain, ApiKeyHasher.Hash(plain));

        await store.RevokeAsync(created.Id);
        var auth = await validator.ValidateAsync(plain);
        auth.Should().BeNull();
    }

    [Fact]
    public async Task TC_SEC_004_AdminRole_HasAllPermissions()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresApiKeyStore(db);
        var plain = ApiKeyHasher.GeneratePlainKey();
        await store.CreateAsync(new CreateApiKeyRequest
        {
            Name = "admin", Roles = [AppRoles.Admin]
        }, plain, ApiKeyHasher.Hash(plain));

        var auth = await new ApiKeyValidator(store).ValidateAsync(plain);
        auth!.IsAdmin.Should().BeTrue();
        auth.HasRole(AppRoles.Sender).Should().BeTrue();
        auth.HasAnyRole(AppRoles.Reader, AppRoles.Sender).Should().BeTrue();
    }

    [Fact]
    public void TC_SEC_005_Hasher_IsDeterministicAndNotPlain()
    {
        var plain = "nh_test_key_value";
        var h1 = ApiKeyHasher.Hash(plain);
        var h2 = ApiKeyHasher.Hash(plain);
        h1.Should().Be(h2);
        h1.Should().NotBe(plain);
        h1.Length.Should().Be(64); // sha256 hex
    }
}
