using DocuMind.Application.Abstractions;
using DocuMind.Application.Common;
using DocuMind.Infrastructure.Auth;
using DocuMind.Infrastructure.Identity;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services,IConfiguration config)
        {
            services.Configure<EmbeddingOptions>(config.GetSection(EmbeddingOptions.SectionName));

            services.AddOptions<JwtOptions>()
                .Bind(config.GetSection(JwtOptions.SectionName))
                .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey msut be at least 32 characters.")
                .ValidateOnStart();

            services.AddScoped<TenantContext>();
            services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<TenantContext>());
            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(config.GetConnectionString("Postgres"), npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention());

            services.AddIdentityCore<ApplicationUser>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 10;
                o.Password.RequireNonAlphanumeric = false;
                o.Lockout.MaxFailedAccessAttempts = 5;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<AppDbContext>();

            services.AddScoped<TokenService>();
            services.AddScoped<IAuthService, AuthService>();

            return services;
        }
    }
}
