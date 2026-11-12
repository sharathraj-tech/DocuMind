using DocuMind.Application.Abstractions;
using DocuMind.Application.Common;
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

            services.AddScoped<TenantContext>();
            services.AddScoped<ICurrentTenant>(sp => sp.GetRequiredService<TenantContext>());
            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(config.GetConnectionString("Postgres"), npgsql => npgsql.UseVector())
            .UseSnakeCaseNamingConvention());

            return services;
        }
    }
}
