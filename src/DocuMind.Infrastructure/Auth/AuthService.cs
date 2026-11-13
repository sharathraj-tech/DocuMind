using DocuMind.Application.Abstractions;
using DocuMind.Application.Auth;
using DocuMind.Application.Common;
using DocuMind.Domain.Entities;
using DocuMind.Domain.Enums;
using DocuMind.Infrastructure.Identity;
using DocuMind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace DocuMind.Infrastructure.Auth
{
    public sealed class AuthService(
        AppDbContext db,
        UserManager<ApplicationUser> users,
        TokenService tokens,
        IOptions<JwtOptions> jwt) : IAuthService
    {
        private const string InvalidCredentials = "Invalid email or password";
        private const string InvalidRefresh = "Invalid or expired refresh token";

        public async Task<Result<AuthResponse>> RegisterAsync(RegisterRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Email) || !req.Email.Contains("@"))
                return Result<AuthResponse>.Fail("A valid email is required.", 400);
            if (string.IsNullOrWhiteSpace(req.WorkspaceName))
                return Result<AuthResponse>.Fail("A workspace name is required.", 400);

            await using var tx = await db.Database.BeginTransactionAsync(ct);

            var email = req.Email.Trim();
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                DisplayName = req.DisplayName?.Trim()
            };

            var created = await users.CreateAsync(user, req.Password);
            if(!created.Succeeded)
            {
                var duplicate = created.Errors.Any(e => e.Code is "DuplicateUserName" or "DuplicateEmail");
                return Result<AuthResponse>.Fail(
                    string.Join(" ", created.Errors.Select(e => e.Description)),
                    duplicate ? 409 : 400);
            }

            var name = req.WorkspaceName.Trim();
            var tenant = new Tenant { Name = name, Slug = await UniqueSlugAsync(name, ct) };
            tenant.Members.Add(new TenantMember { UserId = user.Id, Role = TenantRole.Admin });
            db.Tenants.Add(tenant);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            return Result<AuthResponse>.Ok(await IssueAsync(user, tenant.Id, TenantRole.Admin, ct));
        }

        public async Task<Result<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct)
        {
            var user = await users.FindByEmailAsync(req.Email ?? string.Empty);

            if (user is null)
                return Result<AuthResponse>.Fail(InvalidCredentials, 401);

            if (await users.IsLockedOutAsync(user))
                return Result<AuthResponse>.Fail("Account temporaily locked. Try again later.", 423);

            if(!await users.CheckPasswordAsync(user,req.Password??string.Empty))
            {
                await users.AccessFailedAsync(user);
                return Result<AuthResponse>.Fail(InvalidCredentials, 401);
            }

            await users.ResetAccessFailedCountAsync(user);

            var memberships = await db.TenantMembers
                .Where(m => m.UserId == user.Id)
                .OrderBy(m => m.JoinedAt)
                .ToListAsync(ct);

            var membership = req.TenantId is { } wanted
                ? memberships.FirstOrDefault(m => m.TenantId == wanted)
                : memberships.FirstOrDefault();

            if (membership is null)
                return Result<AuthResponse>.Fail("no access to that workspace.", 403);

            return Result<AuthResponse>.Ok(await IssueAsync(user, membership.TenantId, membership.Role, ct));
        }

        public async Task<Result<AuthResponse>> RefreshAsync(RefreshRequest req,CancellationToken ct)
        {
            var hash = TokenService.Hash(req.RefreshToken ?? string.Empty);
            var stored = await db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

            if (stored is null)
                return Result<AuthResponse>.Fail(InvalidRefresh, 401);

            if(stored.RevokedAt is not null)
            {
                await db.RefreshTokens
                    .Where(t=>t.UserId == stored.UserId && t.RevokedAt ==null)
                    .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
                return Result<AuthResponse>.Fail(InvalidRefresh, 401);
            }

            if (stored.ExpiresAt <= DateTime.UtcNow)
                return Result<AuthResponse>.Fail(InvalidRefresh, 401);

            var user = await users.FindByIdAsync(stored.UserId.ToString());
            var membership = await db.TenantMembers
                .FirstOrDefaultAsync(m => m.TenantId == stored.TenantId && m.UserId == stored.UserId, ct);

            if (user is null || membership is null)
                return Result<AuthResponse>.Fail(InvalidRefresh, 401);

            // Rotate: revoke the old token and issue a new one in the same SaveChanges.
            stored.RevokedAt = DateTime.UtcNow;
            return Result<AuthResponse>.Ok(await IssueAsync(user, stored.TenantId, membership.Role, ct));
        }

        public async Task LogoutAsync(RefreshRequest req, CancellationToken ct)
        {
            var hash = TokenService.Hash(req.RefreshToken ?? string.Empty);
            await db.RefreshTokens
                .Where(t => t.TokenHash == hash && t.RevokedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, DateTime.UtcNow), ct);
        }

        private async Task<AuthResponse> IssueAsync(ApplicationUser user, Guid tenantId, TenantRole role, CancellationToken ct)
        {
            var (access, expires) = tokens.CreateAccessToken(user, tenantId, role);
            var refresh = tokens.CreateRefreshToken();

            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                TenantId = tenantId,
                TokenHash = refresh.Hash,
                ExpiresAt = DateTime.UtcNow.AddDays(jwt.Value.RefreshTokenDays)
            });
            await db.SaveChangesAsync(ct);

            return new AuthResponse(access, refresh.Token, expires, tenantId, role.ToString());
        }

        private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
        {
            var baseSlug = Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            if (baseSlug.Length == 0) baseSlug = "workspace";
            if (baseSlug.Length > 80) baseSlug = baseSlug[..80];

            var slug = baseSlug;
            while (await db.Tenants.AnyAsync(t => t.Slug == slug, ct))
                slug = $"{baseSlug}-{Guid.NewGuid().ToString("N")[..6]}";
            return slug;
        }
    }
}
