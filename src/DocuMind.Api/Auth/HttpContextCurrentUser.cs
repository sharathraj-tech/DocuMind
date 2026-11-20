using DocuMind.Application.Abstractions;
using System.Security.Claims;

namespace DocuMind.Api.Auth;

public sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal User => httpContextAccessor.HttpContext?.User ?? new ClaimsPrincipal();

    public Guid? UserId => Guid.TryParse(User.FindFirst("sub")?.Value, out var userId)
        ? userId
        : null;

    public bool IsAdmin => string.Equals(User.FindFirst("role")?.Value, "Admin", StringComparison.OrdinalIgnoreCase);
}
