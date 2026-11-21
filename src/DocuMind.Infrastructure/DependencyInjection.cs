using DocuMind.Application.Abstractions;
using DocuMind.Application.Common;
using DocuMind.Application.Ingestion;
using DocuMind.Infrastructure.AI;
using DocuMind.Infrastructure.Auth;
using DocuMind.Infrastructure.Chat;
using DocuMind.Infrastructure.Chunking;
using DocuMind.Infrastructure.Documents;
using DocuMind.Infrastructure.Identity;
using DocuMind.Infrastructure.Ingestion;
using DocuMind.Infrastructure.Parsing;
using DocuMind.Infrastructure.Persistence;
using DocuMind.Infrastructure.Search;
using DocuMind.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services,IConfiguration config)
        {
            services.AddOptions<JwtOptions>()
                .Bind(config.GetSection(JwtOptions.SectionName))
                .Validate(o => o.SigningKey.Length >= 32, "Jwt:SigningKey msut be at least 32 characters.")
                .ValidateOnStart();

            services.AddOptions<ChunkingOptions>()
                .Bind(config.GetSection(ChunkingOptions.SectionName))
                .Validate(o => o.TargetTokens >= 50 && o.OverlapTokens >= 0 && o.OverlapTokens < o.TargetTokens,
                    "Chunking: TargetTokens must be at least 50 and OverlapTokens must be smaller than TargetTokens.")
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

            services.AddOptions<StorageOptions>()
                .Bind(config.GetSection(StorageOptions.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.ConnectionString), "Storage:ConnectionString is required.")
                .ValidateOnStart();

            services.AddSingleton<IFileStorage,AzureBlobFileStorage>();

            services.AddSingleton<ChannelIngestionQueue>();
            services.AddSingleton<IIngestionQueue>(sp => sp.GetRequiredService<ChannelIngestionQueue>());
            services.AddScoped<IDocumentIngestor, PlaceholderIngestor>();
            services.AddHostedService<IngestionWorker>();
            services.AddScoped<IDocumentService, DocumentService>();

            services.AddSingleton<ITokenCounter, ApproximateTokenCounter>();
            services.AddSingleton<IChunker, RecursiveChunker>();

            services.AddSingleton<IDocumentParser, PdfParser>();
            services.AddSingleton<IDocumentParser, DocxParser>();
            services.AddSingleton<IDocumentParser, MarkDownParsers>();
            services.AddSingleton<IDocumentParser, HtmlParser>();
            services.AddSingleton<IDocumentParser, PlainTextParser>();
            services.AddScoped<IDocumentIngestor, DocumentIngestor>();

            services.AddOptions<EmbeddingOptions>()
    .Bind(config.GetSection(EmbeddingOptions.SectionName))
    .Validate(o => o.Dimensions is >= 1 and <= 2000 && o.BatchSize is >= 1 and <= 100 && !string.IsNullOrWhiteSpace(o.Model),
              "Embedding: Dimensions must be 1-2000, BatchSize 1-100, and Model must be set.")
    .ValidateOnStart();

            services.AddOptions<GeminiOptions>().Bind(config.GetSection(GeminiOptions.SectionName));

            AddGeminiHttpClient<GeminiEmbeddingGenerator>(services);
            AddGeminiHttpClient<GeminiChatClient>(services);

            services.AddTransient<IEmbeddingGenerator<string, Embedding<float>>>(
                sp => sp.GetRequiredService<GeminiEmbeddingGenerator>());
            services.AddTransient<IChatClient>(sp => sp.GetRequiredService<GeminiChatClient>());

            services.AddOptions<ChatSettings>()
                .Bind(config.GetSection(ChatSettings.SectionName))
                .Validate(o => !string.IsNullOrWhiteSpace(o.Model)
                               && !string.IsNullOrWhiteSpace(o.RewriteModel)
                               && o.TopK is >= 1 and <= 20
                               && o.ContextTokenBudget >= 500
                               && o.HistoryMessages is >= 0 and <= 20
                               && o.MaxOutputTokens >= 100,
                          "Chat settings are invalid (models required, TopK 1-20, ContextTokenBudget >= 500, HistoryMessages 0-20, MaxOutputTokens >= 100).")
                .ValidateOnStart();

            services.AddScoped<IChatService, ChatService>();
            services.AddScoped<IConversationService, ConversationService>();

            services.AddScoped<ISearchService, SearchService>();

            return services;
        }

        private static void AddGeminiHttpClient<T>(IServiceCollection services) where T : class
        {
            services.AddHttpClient<T>((sp, client) =>
            {
                client.BaseAddress = new Uri(sp.GetRequiredService<IOptions<GeminiOptions>>().Value.BaseUrl);
            })
                .AddStandardResilienceHandler(o =>
                {
                    o.Retry.MaxRetryAttempts = 5;                           // 429s and 5xx are retried with backoff
                    o.Retry.Delay = TimeSpan.FromSeconds(2);
                    o.Retry.UseJitter = true;
                    o.Retry.ShouldRetryAfterHeader = true;
                    o.AttemptTimeout.Timeout = TimeSpan.FromSeconds(60);
                    o.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(150);   // at least 2x the attempt timeout
                    o.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
                });
        }
    }
}
