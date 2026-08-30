using NotificationHub.Core.Identity;
using NotificationHub.Host.Middleware;

namespace NotificationHub.Host.Auth;

public static class AccountAuthEndpoints
{
    public static IEndpointRouteBuilder MapAccountAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/v1/auth").WithTags("Auth");

        g.MapPost("/register", async (RegisterRequest body, AccountAuthService auth, CancellationToken ct) =>
        {
            var (ok, error, tokens) = await auth.RegisterAsync(
                body.Email, body.Password, body.DisplayName,
                body.CreateOrganization ?? true, body.OrganizationName, ct);
            if (!ok)
                return Results.BadRequest(new { error });
            return Results.Ok(tokens);
        }).AllowAnonymous().WithName("AuthRegister");

        g.MapPost("/login", async (LoginRequest body, AccountAuthService auth, CancellationToken ct) =>
        {
            var (ok, error, tokens) = await auth.LoginAsync(
                body.Email, body.Password, body.OrganizationId, ct);
            if (!ok)
                return Results.Json(new { error }, statusCode: StatusCodes.Status401Unauthorized);
            return Results.Ok(tokens);
        }).AllowAnonymous().WithName("AuthLogin");

        g.MapPost("/refresh", async (RefreshRequest body, AccountAuthService auth, CancellationToken ct) =>
        {
            var (ok, tokens) = await auth.RefreshAsync(body.RefreshToken, ct);
            if (!ok || tokens is null)
                return Results.Unauthorized();
            return Results.Ok(tokens);
        }).AllowAnonymous().WithName("AuthRefresh");

        return app;
    }

    public sealed record RegisterRequest(
        string Email,
        string Password,
        string? DisplayName,
        bool? CreateOrganization,
        string? OrganizationName);

    public sealed record LoginRequest(string Email, string Password, Guid? OrganizationId);
    public sealed record RefreshRequest(string RefreshToken);
}
