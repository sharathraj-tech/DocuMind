using DocuMind.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using Testcontainers.PostgreSql;

namespace DocuMind.IntegrationTests
{
    public class ApiFactory:WebApplicationFactory<Program>, IAsyncLifetime
    {
        private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .Build();

        public async Task InitializeAsync()
        {
            await _pg.StartAsync();
            using var scope = Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseSetting("ConnectingStrings:Postgres", _pg.GetConnectionString());
            builder.UseSetting("Jwt:Issuer", "documind-tests");
            builder.UseSetting("Jwt:Audience", "documind-tests-api");
            builder.UseSetting("Jwt:SigningKey", "kd!f:&aY+ohfsV.#,>_%NylX/6ONXE*fN+bRAek0Jk]gn<;Tz]qG*VlG)ooBouJ@");

        }
        async Task IAsyncLifetime.DisposeAsync()
        {
            await base.DisposeAsync();
            await _pg.DisposeAsync();
        }
    }
}
