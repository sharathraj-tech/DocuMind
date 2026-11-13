using DocuMind.Application.Auth;
using DocuMind.Application.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Abstractions
{
    public interface IAuthService
    {
        Task<Result<AuthResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct);
        Task<Result<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken ct);
        Task<Result<AuthResponse>> RefreshAsync(RefreshRequest request, CancellationToken ct);
        Task LogoutAsync(RefreshRequest request, CancellationToken ct);
    }
}
