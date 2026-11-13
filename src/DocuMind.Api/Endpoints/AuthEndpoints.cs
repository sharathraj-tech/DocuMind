using DocuMind.Application.Abstractions;
using DocuMind.Application.Auth;
using DocuMind.Application.Common;
using System.Security.Claims;

namespace DocuMind.Api.Endpoints
{
    public static class AuthEndpoints
    {
        public static async Task<IEndpointRouteBuilder> MapAuthEndPoints(this IEndpointRouteBuilder app)
        {
            var g = app.MapGroup("/api/auth").WithTags("Auth");

            g.MapPost("/register", async (RegisterRequest r, IAuthService auth, CancellationToken ct) =>
            ToHttp(await auth.RegisterAsync(r, ct))).AllowAnonymous();

            g.MapPost("/login", async (LoginRequest r, IAuthService auth, CancellationToken ct) =>
                ToHttp(await auth.LoginAsync(r, ct))).AllowAnonymous();

            g.MapPost("/refresh", async (RefreshRequest r, IAuthService auth, CancellationToken ct) =>
                ToHttp(await auth.RefreshAsync(r, ct))).AllowAnonymous();

            g.MapPost("/logout", async (RefreshRequest r, IAuthService auth, CancellationToken ct) =>
            {
                await auth.LogoutAsync(r, ct);
                return Results.NoContent();
            }).AllowAnonymous();

            g.MapGet("/me", (ClaimsPrincipal user) => Results.Ok(new CurrentUserDto(
                Guid.Parse(user.FindFirstValue("sub")!),
                user.FindFirstValue("email"),
                Guid.Parse(user.FindFirstValue("tenant_id")!),
                user.FindFirstValue("role")!)));

            return app;
        }

        private static IResult ToHttp(Result<AuthResponse> result) =>
        result.Succeeded
            ? Results.Ok(result.Value)
            : Results.Problem(title: result.Error, statusCode: result.Status);
    }
}
