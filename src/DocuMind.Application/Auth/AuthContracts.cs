using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Auth
{
    public sealed record RegisterRequest(string Email, string Password, string? DisplayName, string WorkspaceName);
    public sealed record LoginRequest(string Email, string Password, Guid? TenantId = null);
    public sealed record RefreshRequest(string RefreshToken);

    public sealed record AuthResponse(
        string AccessToken,
        string RefreshToken,
        DateTime AccessTokenExpiresAt,
        Guid TenantId,
        string Role);

    public sealed record CurrentUserDto(Guid UserId, string? Email, Guid TenantId, string Role);
}
