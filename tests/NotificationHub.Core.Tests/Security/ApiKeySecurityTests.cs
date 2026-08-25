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
        var id = Guid.NewGuid();
        var plain = ApiKeyHasher.GeneratePlainKey(id);
        var hash = ApiKeyHasher.Hash(plain);

        var created = await store.CreateAsync(new CreateApiKeyRequest
        {
            Name = "tenant-a-sender",
            TenantId = "tenant-a",
            Roles = [AppRoles.Sender, AppRoles.Reader]
        }, plain, hash);

        created.PlainKey.Should().Be(plain);
        created.TenantId.Should().Be("tenant-a");
        created.Id.Should().Be(id);

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
        var id = Guid.NewGuid();
        var plain = ApiKeyHasher.GeneratePlainKey(id);
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
        var id = Guid.NewGuid();
        var plain = ApiKeyHasher.GeneratePlainKey(id);
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
    public void TC_SEC_005_Hasher_Pbkdf2_NotPlain_AndVerifies()
    {
        var id = Guid.NewGuid();
        var plain = ApiKeyHasher.GeneratePlainKey(id);
        var h1 = ApiKeyHasher.Hash(plain);
        var h2 = ApiKeyHasher.Hash(plain);
        h1.Should().StartWith(ApiKeyHasher.V2Prefix);
        h1.Should().NotBe(h2); // unique salt per hash
        h1.Should().NotBe(plain);
        ApiKeyHasher.Verify(plain, h1).Should().BeTrue();
        ApiKeyHasher.Verify(plain + "x", h1).Should().BeFalse();
    }

    [Fact]
    public async Task TC_SEC_006_LegacySha256_StillValidates()
    {
        await using var db = TestFixtures.CreateDbContext();
        var store = new PostgresApiKeyStore(db);
        var plain = ApiKeyHasher.GeneratePlainKey(); // no embedded id
        var hash = ApiKeyHasher.HashLegacySha256(plain);
        await store.CreateAsync(new CreateApiKeyRequest { Name = "legacy", Roles = [AppRoles.Reader] }, plain, hash);

        var auth = await new ApiKeyValidator(store).ValidateAsync(plain);
        auth.Should().NotBeNull();
    }
}
