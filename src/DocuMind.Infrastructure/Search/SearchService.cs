using DocuMind.Application.Abstractions;
using DocuMind.Application.Common;
using DocuMind.Application.Search;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Search
{
    public sealed class SearchService(
    AppDbContext db,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    ILogger<SearchService> log) : ISearchService
    {
        private const int MaxQueryLength = 2000;

        public async Task<Result<IReadOnlyList<SearchHit>>> SearchAsync(SearchRequest request, CancellationToken ct)
        {
            var query = request.Query?.Trim();
            if (string.IsNullOrEmpty(query))
                return Result<IReadOnlyList<SearchHit>>.Fail("A query is required.", 400);
            if (query.Length > MaxQueryLength)
                return Result<IReadOnlyList<SearchHit>>.Fail($"The query is longer than {MaxQueryLength} characters.", 400);

            var topK = Math.Clamp(request.TopK ?? 8, 1, 20);

            Vector queryVector;
            try
            {
                var generated = await embeddings.GenerateAsync(
                    [query],
                    new EmbeddingGenerationOptions
                    {
                        AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            [EmbeddingTaskTypes.Key] = EmbeddingTaskTypes.Query
                        }
                    },
                    ct);
                queryVector = new Vector(generated[0].Vector.ToArray());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogError(ex, "Embedding the search query failed");
                return Result<IReadOnlyList<SearchHit>>.Fail("The embedding service is unavailable. Try again shortly.", 503);
            }

            // With a tenant filter on top of an HNSW index, plain ANN search can return too few rows
            // (it filters after fetching its candidates). Iterative scans (pgvector 0.8+) keep scanning until
            // enough rows pass the filter. SET LOCAL only lasts for this transaction.
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("SET LOCAL hnsw.iterative_scan = strict_order", ct);

            // The tenant query filter on DocumentChunk applies automatically.
            var rows = await db.DocumentChunks
                .Where(c => c.Embedding != null)
                .OrderBy(c => c.Embedding!.CosineDistance(queryVector))
                .Take(topK)
                .Select(c => new
                {
                    c.Id,
                    c.DocumentId,
                    DocumentName = c.Document.FileName,
                    c.PageNumber,
                    c.ChunkIndex,
                    c.Content,
                    Distance = c.Embedding!.CosineDistance(queryVector)
                })
                .ToListAsync(ct);

            await tx.CommitAsync(ct);

            IReadOnlyList<SearchHit> hits = rows
                .Select(r => new SearchHit(r.Id, r.DocumentId, r.DocumentName, r.PageNumber, r.ChunkIndex, r.Content, 1 - r.Distance))
                .ToList();

            return Result<IReadOnlyList<SearchHit>>.Ok(hits);
        }
    }
}
