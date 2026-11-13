using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Auth
{
    public class JwtOptions
    {
        public const string SectionName = "Jwt";
        public string Issuer { get; set; } = string.Empty;
        public string Audience { get; set; } = string.Empty;
        public string SigningKey { get; set; } = string.Empty;
        public int AccessTokenMinutes { get; set; } = 15;
        public int RefreshTokenDays { get; set; } = 14;

        public SymmetricSecurityKey CreateKey() => new(Encoding.UTF8.GetBytes(SigningKey));
    }
}
