using Microsoft.EntityFrameworkCore;
using NotificationHub.Core.Persistence;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

var cs = builder.Configuration.GetConnectionString("Default")
         ?? builder.Configuration["ConnectionStrings:Default"]
         ?? throw new InvalidOperationException("ConnectionStrings:Default required for Identity host");

builder.Services.AddDbContext<NotificationDbContext>(o =>
{
    o.UseNpgsql(cs);
    o.UseOpenIddict();
});

builder.Services.AddOpenIddict()
    .AddCore(o => o.UseEntityFrameworkCore().UseDbContext<NotificationDbContext>())
    .AddServer(o =>
    {
        o.SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetUserinfoEndpointUris("/connect/userinfo")
            .SetLogoutEndpointUris("/connect/logout");

        o.AllowAuthorizationCodeFlow()
            .RequireProofKeyForCodeExchange()
            .AllowRefreshTokenFlow()
            .AllowClientCredentialsFlow();

        o.RegisterScopes(
            Scopes.OpenId, Scopes.Profile, Scopes.Email,
            "notificationhub.admin");

        o.AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        o.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            .EnableUserinfoEndpointPassthrough()
            .EnableLogoutEndpointPassthrough();
    })
    .AddValidation(o =>
    {
        o.UseLocalServer();
        o.UseAspNetCore();
    });

builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddControllersWithViews();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<NotificationDbContext>();
    await db.Database.EnsureCreatedAsync();
    await SeedClientsAsync(scope.ServiceProvider);
}

app.UseDeveloperExceptionPage();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/", () => Results.Ok(new { service = "NotificationHub.Identity", status = "ok" }));

app.Run();

static async Task SeedClientsAsync(IServiceProvider sp)
{
    var manager = sp.GetRequiredService<IOpenIddictApplicationManager>();
    if (await manager.FindByClientIdAsync("admin-ui") is null)
    {
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "admin-ui",
            DisplayName = "NotificationHub Admin UI",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            RedirectUris = { new Uri("http://localhost:3000/api/auth/callback") },
            PostLogoutRedirectUris = { new Uri("http://localhost:3000/") },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Logout,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Scopes.Roles,
                Permissions.Prefixes.Scope + "notificationhub.admin"
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange }
        });
    }
}
