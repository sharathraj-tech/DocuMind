using DocuMind.Application.Abstractions;
using DocuMind.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace DocuMind.Api.Endpoints
{
    public static class DocumentEndpoints
    {
        public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
        {
            var g = app.MapGroup("/api/documents").WithTags("Documents").RequireAuthorization();

            // JWT bearer API with no cookies, so antiforgery doesn't apply to the multipart upload.
            g.MapPost("/", async ([FromForm(Name = "file")] IFormFile? file, IDocumentService docs, CancellationToken ct) =>
            {
                if (file is null || file.Length == 0)
                    return Results.BadRequest("A non-empty form file named 'file' is required.");

                await using var stream = file.OpenReadStream();
                var result = await docs.UploadAsync(file.FileName, stream, ct);
                return result.Succeeded
                    ? Results.Created($"/api/documents/{result.Value!.Id}", result.Value)
                    : Problem(result);
            }).DisableAntiforgery();

            g.MapGet("/", async (int? skip, int? take, IDocumentService docs, CancellationToken ct) =>
                Results.Ok(await docs.ListAsync(skip ?? 0, take ?? 50, ct)));

            g.MapGet("/{id:guid}", async (Guid id, IDocumentService docs, CancellationToken ct) =>
            {
                var result = await docs.GetAsync(id, ct);
                return result.Succeeded ? Results.Ok(result.Value) : Problem(result);
            });

            g.MapGet("/{id:guid}/file", async (Guid id, HttpContext http, IDocumentService docs, CancellationToken ct) =>
            {
                var result = await docs.OpenFileAsync(id, ct);
                if (!result.Succeeded) return Problem(result);

                http.Response.Headers["X-Content-Type-Options"] = "nosniff";
                http.Response.Headers["Content-Security-Policy"] = "sandbox; default-src 'none'";

                return Results.Stream(result.Value!.Content, result.Value.ContentType, enableRangeProcessing: true);
            });

            g.MapDelete("/{id:guid}", async (Guid id, IDocumentService docs, CancellationToken ct) =>
            {
                var result = await docs.DeleteAsync(id, ct);
                return result.Succeeded ? Results.NoContent() : Problem(result);
            });

            g.MapPost("/{id:guid}/reindex", async (Guid id, IDocumentService docs, CancellationToken ct) =>
            {
                var result = await docs.ReindexAsync(id, ct);
                return result.Succeeded ? Results.Accepted($"/api/documents/{id}", result.Value) : Problem(result);
            });

            g.MapPost("/reindex-outdated", async (IDocumentService docs, CancellationToken ct) =>
            {
                var result = await docs.ReindexOutdatedAsync(ct);
                return result.Succeeded ? Results.Ok(new { queued = result.Value }) : Problem(result);
            });

            return app;
        }

        private static IResult Problem<T>(Result<T> result)
        => Results.Problem(title: result.Error, statusCode: result.Status);
    }
}
