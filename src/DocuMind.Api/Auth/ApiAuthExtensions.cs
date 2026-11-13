using DocuMind.Infrastructure.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace DocuMind.Api.Auth
{
    public static class ApiAuthExtensions
    {
        public static IServiceCollection AddApiAuth(this IServiceCollection services)
        {
            services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

            services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
                .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
                {
                    var j = jwt.Value;
                    bearer.MapInboundClaims = false;
                    bearer.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = j.Issuer,
                        ValidateAudience = true,
                        ValidAudience = j.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = j.CreateKey(),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = "sub",
                        RoleClaimType = "role"
                    };
                });

            services.AddAuthorization(o =>
            o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

            return services;
        }
    }
}
