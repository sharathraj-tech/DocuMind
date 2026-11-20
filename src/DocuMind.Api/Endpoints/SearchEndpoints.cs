using DocuMind.Application.Abstractions;
using DocuMind.Application.Search;

namespace DocuMind.Api.Endpoints
{
    public static class SearchEndpoints
    {
        public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder app)
        {
            app.MapPost("/api/search", async (SearchRequest request, ISearchService search, CancellationToken ct) =>
            {
                var result = await search.SearchAsync(request, ct);
                return result.Succeeded
                    ? Results.Ok(result.Value)
                    : Results.Problem(title: result.Error, statusCode: result.Status);
            }).WithTags("Search").RequireAuthorization();

            return app;
        }
    }
}
