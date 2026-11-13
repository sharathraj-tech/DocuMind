using DocuMind.Domain.Enums;
using DocuMind.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Text;
using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using System.Security.Cryptography;

namespace DocuMind.Infrastructure.Auth
{
    public sealed class TokenService(IOptions<JwtOptions> options)
    {
        private readonly JwtOptions _o = options.Value;

        public(string Token,DateTime ExpiresAt) CreateAccessToken(ApplicationUser user, Guid tenantId, TenantRole role)
        {
            var expires = DateTime.UtcNow.AddMinutes(_o.AccessTokenMinutes);

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _o.Issuer,
                Audience = _o.Audience,
                Expires = expires,
                SigningCredentials = new SigningCredentials(_o.CreateKey(), SecurityAlgorithms.HmacSha256),
                Subject = new ClaimsIdentity(
                    [
                        new Claim("sub",user.Id.ToString()),
                        new Claim("email",user.Email?? string.Empty),
                        new Claim("tenant_id",tenantId.ToString()),
                        new Claim("role",role.ToString())
                    ])
            };

            return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
        }

        public (string Token, string Hash) CreateRefreshToken()
        {
            var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            return (token, Hash(token));
        }

        public static string Hash(string token)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
