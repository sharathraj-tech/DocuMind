using DocuMind.Infrastructure;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Scalar;
using Scalar.AspNetCore;
using DocuMind.Api.Auth;
using DocuMind.Api.Middleware;
using DocuMind.Api.Endpoints;
using Microsoft.OpenApi;
using DocuMind.Application.Abstractions;
using Microsoft.AspNetCore.Http.Features;
using DocuMind.Application.Documents;
using Azure.Storage.Blobs;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.WriteTo.Console());
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiAuth();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Enter a JWT bearer token."
        };

        return Task.CompletedTask;
    });
});
builder.Services.AddSingleton(x => new BlobServiceClient(builder.Configuration["Storage:ConnectionString"]));


const long bodyLimit = UploadPolicy.MaxBytes + 1024 * 1024;
builder.Services.Configure<FormOptions>(o=>o.MultipartBodyLengthLimit = bodyLimit);
builder.WebHost.ConfigureKestrel(o=>o.Limits.MaxRequestBodySize=bodyLimit);

var app = builder.Build();
// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.AddPreferredSecuritySchemes("Bearer");
    }).AllowAnonymous();
}
app.UseAuthentication();
app.UseMiddleware<TenantResolutionMiddleware>();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
app.MapAuthEndPoints();
app.MapDocumentEndpoints();
app.MapSearchEndpoints();
app.MapChatEndpoints();


app.Run();