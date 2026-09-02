using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NotificationHub.Core.Identity;
using NotificationHub.Core.Persistence;

namespace NotificationHub.Host.Auth;

public sealed class JwtTokenOptions
{
    public const string SectionName = "Auth:Jwt";
    public string Issuer { get; set; } = "notificationhub";
    public string Audience { get; set; } = "notificationhub-api";
    /// <summary>Base64 or plain symmetric key (dev). Prefer RSA via OpenIddict in production.</summary>
    public string SigningKey { get; set; } = "NotificationHub-Dev-Signing-Key-Change-Me-32chars!";
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 14;
}

public sealed class AccountAuthService(
    NotificationDbContext db,
    IOptions<JwtTokenOptions> jwtOpts,
    ILogger<AccountAuthService> log)
{
    readonly PasswordHasher<IdentityUserEntity> _hasher = new();
    readonly JwtTokenOptions _jwt = jwtOpts.Value;

    public async Task<(bool Ok, string? Error, AuthTokenResponse? Tokens)> RegisterAsync(
        string email, string password, string? displayName, bool createOrg, string? orgName, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || password.Length < 8)
            return (false, "invalid_input", null);

        if (await db.Set<IdentityUserEntity>().AnyAsync(u => u.Email == email, ct))
            return (false, "email_taken", null);

        var user = new IdentityUserEntity
        {
            Email = email,
            DisplayName = displayName?.Trim() ?? email.Split('@')[0],
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };
        user.PasswordHash = _hasher.HashPassword(user, password);
        db.Set<IdentityUserEntity>().Add(user);

        Guid? orgId = null;
        Guid? membershipId = null;
        var roles = new List<string>();

        if (createOrg)
        {
            var org = new OrganizationEntity
            {
                Name = string.IsNullOrWhiteSpace(orgName) ? $"{user.DisplayName}'s org" : orgName!.Trim(),
                Type = "Merchant",
                Status = "Active"
            };
            db.Set<OrganizationEntity>().Add(org);
            orgId = org.Id;

            var mem = new OrganizationMembershipEntity
            {
                OrganizationId = org.Id,
                UserId = user.Id,
                Status = "Active",
                JoinedAt = DateTimeOffset.UtcNow
            };
            db.Set<OrganizationMembershipEntity>().Add(mem);
            membershipId = mem.Id;

            var ownerRole = await db.Set<IdentityRoleEntity>()
                .FirstOrDefaultAsync(r => r.Name == IdentityRoles.OrganizationOwner, ct)
                ?? await db.Set<IdentityRoleEntity>()
                    .FirstOrDefaultAsync(r => r.Name == IdentityRoles.OrganizationAdmin, ct);
            if (ownerRole is not null)
            {
                db.Set<MembershipRoleEntity>().Add(new MembershipRoleEntity
                {
                    MembershipId = mem.Id,
                    RoleId = ownerRole.Id
                });
                roles.Add(ownerRole.Name);
            }
        }

        await db.SaveChangesAsync(ct);
        log.LogInformation("User registered {UserId}", user.Id);

        var tokens = await IssueTokensAsync(user, orgId, roles, ct);
        return (true, null, tokens);
    }

    public async Task<(bool Ok, string? Error, AuthTokenResponse? Tokens)> LoginAsync(
        string email, string password, Guid? organizationId, CancellationToken ct)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await db.Set<IdentityUserEntity>().FirstOrDefaultAsync(u => u.Email == email, ct);
        if (user is null || string.IsNullOrEmpty(user.PasswordHash))
            return (false, "invalid_credentials", null);

        var verify = _hasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verify == PasswordVerificationResult.Failed)
            return (false, "invalid_credentials", null);

        if (!string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return (false, "user_inactive", null);

        var platformRoles = await (
            from mr in db.Set<MembershipRoleEntity>()
            join m in db.Set<OrganizationMembershipEntity>() on mr.MembershipId equals m.Id
            join r in db.Set<IdentityRoleEntity>() on mr.RoleId equals r.Id
            where m.UserId == user.Id && m.Status == "Active"
            select r.Name).Distinct().ToListAsync(ct);

        Guid? orgId = organizationId;
        var roles = new List<string>(platformRoles);

        if (orgId is null)
        {
            var first = await db.Set<OrganizationMembershipEntity>()
                .Where(m => m.UserId == user.Id && m.Status == "Active")
                .OrderBy(m => m.JoinedAt)
                .FirstOrDefaultAsync(ct);
            orgId = first?.Id is null ? null : first.OrganizationId;
        }

        if (orgId is not null)
        {
            var memRoles = await (
                from mr in db.Set<MembershipRoleEntity>()
                join m in db.Set<OrganizationMembershipEntity>() on mr.MembershipId equals m.Id
                join r in db.Set<IdentityRoleEntity>() on mr.RoleId equals r.Id
                where m.UserId == user.Id && m.OrganizationId == orgId && m.Status == "Active"
                select r.Name).Distinct().ToListAsync(ct);
            roles = memRoles.Count > 0 ? memRoles.ToList() : roles;
        }

        var tokens = await IssueTokensAsync(user, orgId, roles, ct);
        return (true, null, tokens);
    }

    public async Task<(bool Ok, AuthTokenResponse? Tokens)> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = Hash(refreshToken);
        var session = await db.Set<UserSessionEntity>()
            .FirstOrDefaultAsync(s => s.RefreshTokenHash == hash && s.IsActive, ct);
        if (session is null || session.ExpiresAt < DateTimeOffset.UtcNow)
            return (false, null);

        var user = await db.Set<IdentityUserEntity>().FirstOrDefaultAsync(u => u.Id == session.UserId, ct);
        if (user is null || !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return (false, null);

        session.IsActive = false;
        session.RevokedAt = DateTimeOffset.UtcNow;

        var roles = await (
            from mr in db.Set<MembershipRoleEntity>()
            join m in db.Set<OrganizationMembershipEntity>() on mr.MembershipId equals m.Id
            join r in db.Set<IdentityRoleEntity>() on mr.RoleId equals r.Id
            where m.UserId == user.Id && m.Status == "Active"
                  && (session.OrganizationId == null || m.OrganizationId == session.OrganizationId)
            select r.Name).Distinct().ToListAsync(ct);

        var tokens = await IssueTokensAsync(user, session.OrganizationId, roles, ct);
        return (true, tokens);
    }


    public async Task<(bool Ok, string? Error, AuthTokenResponse? Tokens)> ReissueForOrganizationAsync(
        Guid userId, Guid organizationId, CancellationToken ct)
    {
        var user = await db.Set<IdentityUserEntity>().FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null || !string.Equals(user.Status, "Active", StringComparison.OrdinalIgnoreCase))
            return (false, "user_inactive", null);

        var mem = await db.Set<OrganizationMembershipEntity>()
            .FirstOrDefaultAsync(m => m.UserId == userId && m.OrganizationId == organizationId && m.Status == "Active", ct);
        if (mem is null)
            return (false, "membership_inactive_or_missing", null);

        var roles = await (
            from mr in db.Set<MembershipRoleEntity>()
            join r in db.Set<IdentityRoleEntity>() on mr.RoleId equals r.Id
            where mr.MembershipId == mem.Id
            select r.Name).Distinct().ToListAsync(ct);

        var tokens = await IssueTokensAsync(user, organizationId, roles, ct);
        return (true, null, tokens);
    }

    async Task<AuthTokenResponse> IssueTokensAsync(
        IdentityUserEntity user, Guid? orgId, IReadOnlyList<string> roles, CancellationToken ct)
    {
        var jti = Guid.NewGuid().ToString("N");
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, jti),
            new("name", user.DisplayName ?? user.Email)
        };
        if (orgId is not null)
            claims.Add(new Claim("tenant_id", orgId.Value.ToString()));

        foreach (var role in roles.Distinct(StringComparer.OrdinalIgnoreCase))
            claims.Add(new Claim("role", role));

        // SuperAdmin → emit every permission claim for clients that read permissions from token
        if (roles.Contains(IdentityRoles.SuperAdmin, StringComparer.OrdinalIgnoreCase)
            || roles.Contains(IdentityRoles.PlatformAdmin, StringComparer.OrdinalIgnoreCase))
        {
            foreach (var p in IdentityPermissions.All)
                claims.Add(new Claim("permission", p));
        }

        // HMAC-SHA256 needs >= 256 bits of key material. Prefer configured key;
        // if shorter, derive 32 bytes via SHA-256 (never use unsafe Substring ranges).
        var keyBytes = Encoding.UTF8.GetBytes(_jwt.SigningKey ?? string.Empty);
        if (keyBytes.Length < 32)
            keyBytes = SHA256.HashData(keyBytes.Length == 0 ? "NotificationHub.DevSigningKey"u8.ToArray() : keyBytes);
        else if (keyBytes.Length > 64)
            keyBytes = keyBytes.AsSpan(0, 64).ToArray();
        var key = new SymmetricSecurityKey(keyBytes);

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: creds);
        var access = new JwtSecurityTokenHandler().WriteToken(token);

        var refresh = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        db.Set<UserSessionEntity>().Add(new UserSessionEntity
        {
            UserId = user.Id,
            OrganizationId = orgId,
            JwtId = jti,
            RefreshTokenHash = Hash(refresh),
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays),
            LastSeenAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(ct);

        return new AuthTokenResponse(access, refresh, (int)TimeSpan.FromMinutes(_jwt.AccessTokenMinutes).TotalSeconds, "Bearer", orgId);
    }

    static string Hash(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed record AuthTokenResponse(string AccessToken, string RefreshToken, int ExpiresIn, string TokenType, Guid? OrganizationId = null);

public sealed class SuperAdminSeedOptions
{
    public const string SectionName = "Auth:SuperAdmin";
    public string Email { get; set; } = "superadmin@notificationhub.local";
    public string Password { get; set; } = "ChangeMe!SuperAdmin1";
    public string DisplayName { get; set; } = "Super Admin";
}

public static class SuperAdminSeeder
{
    public static async Task EnsureAsync(IServiceProvider sp, CancellationToken ct = default)
    {
        // DbContext is scoped — never resolve from root provider (app.Services).
        await using var scope = sp.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<NotificationDbContext>();
        var opts = services.GetRequiredService<IOptions<SuperAdminSeedOptions>>().Value;
        var log = services.GetRequiredService<ILoggerFactory>().CreateLogger("SuperAdminSeeder");

        await IdentitySchema.EnsureAsync(db, log, ct);

        var email = opts.Email.Trim().ToLowerInvariant();
        var user = await db.Set<IdentityUserEntity>().FirstOrDefaultAsync(u => u.Email == email, ct);
        var hasher = new PasswordHasher<IdentityUserEntity>();

        if (user is null)
        {
            user = new IdentityUserEntity
            {
                Email = email,
                DisplayName = opts.DisplayName,
                Status = "Active"
            };
            user.PasswordHash = hasher.HashPassword(user, opts.Password);
            db.Set<IdentityUserEntity>().Add(user);
            await db.SaveChangesAsync(ct);
            log.LogWarning("Seeded SuperAdmin user — change password immediately");
        }

        var role = await db.Set<IdentityRoleEntity>()
            .FirstOrDefaultAsync(r => r.Name == IdentityRoles.SuperAdmin, ct);
        if (role is null)
        {
            role = new IdentityRoleEntity { Name = IdentityRoles.SuperAdmin, Scope = "Platform", IsSystem = true };
            db.Set<IdentityRoleEntity>().Add(role);
            await db.SaveChangesAsync(ct);
            foreach (var permName in IdentityPermissions.All)
            {
                var perm = await db.Set<IdentityPermissionEntity>().FirstAsync(p => p.Name == permName, ct);
                db.Set<RolePermissionEntity>().Add(new RolePermissionEntity { RoleId = role.Id, PermissionId = perm.Id });
            }
            await db.SaveChangesAsync(ct);
        }

        // Platform org for SuperAdmin membership
        var platformOrg = await db.Set<OrganizationEntity>()
            .FirstOrDefaultAsync(o => o.Slug == "platform", ct);
        if (platformOrg is null)
        {
            platformOrg = new OrganizationEntity
            {
                Name = "Platform",
                Slug = "platform",
                Type = "Platform",
                Status = "Active"
            };
            db.Set<OrganizationEntity>().Add(platformOrg);
            await db.SaveChangesAsync(ct);
        }

        var mem = await db.Set<OrganizationMembershipEntity>()
            .FirstOrDefaultAsync(m => m.UserId == user.Id && m.OrganizationId == platformOrg.Id, ct);
        if (mem is null)
        {
            mem = new OrganizationMembershipEntity
            {
                UserId = user.Id,
                OrganizationId = platformOrg.Id,
                Status = "Active"
            };
            db.Set<OrganizationMembershipEntity>().Add(mem);
            await db.SaveChangesAsync(ct);
        }

        if (!await db.Set<MembershipRoleEntity>()
                .AnyAsync(mr => mr.MembershipId == mem.Id && mr.RoleId == role.Id, ct))
        {
            db.Set<MembershipRoleEntity>().Add(new MembershipRoleEntity
            {
                MembershipId = mem.Id,
                RoleId = role.Id
            });
            await db.SaveChangesAsync(ct);
        }
    }
}
