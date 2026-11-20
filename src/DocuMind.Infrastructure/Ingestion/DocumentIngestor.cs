using DocuMind.Application.Abstractions;
using DocuMind.Application.Common;
using DocuMind.Application.Ingestion;
using DocuMind.Domain.Entities;
using DocuMind.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Pgvector;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Ingestion
{
    public sealed class DocumentIngestor(
    AppDbContext db,
    IFileStorage storage,
    IEnumerable<IDocumentParser> parsers,
    IChunker chunker,
    IEmbeddingGenerator<string, Embedding<float>> embeddings,
    IOptions<EmbeddingOptions> embeddingOptions,
    ILogger<DocumentIngestor> log) : IDocumentIngestor
    {
        public async Task IngestAsync(Document document, CancellationToken ct)
        {
            var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
            var parser = parsers.FirstOrDefault(p => p.Extensions.Contains(extension))
                         ?? throw new InvalidOperationException($"No parser is available for '{extension}' files.");

            ParsedDocument parsed;
            await using (var stream = await storage.OpenReadAsync(document.BlobName, ct))
                parsed = await parser.ParseAsync(stream, ct);

            var chunks = chunker.Chunk(parsed);
            if (chunks.Count == 0)
                throw new InvalidOperationException("No text could be extracted from this document.");

            var settings = embeddingOptions.Value;
            var vectors = await EmbedAsync(document, chunks, settings, ct);

            // Replace the old chunks and add the new ones as a single unit.
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            await db.DocumentChunks.Where(c => c.DocumentId == document.Id).ExecuteDeleteAsync(ct);

            for (var i = 0; i < chunks.Count; i++)
            {
                db.DocumentChunks.Add(new DocumentChunk
                {
                    TenantId = document.TenantId,
                    DocumentId = document.Id,
                    ChunkIndex = chunks[i].Index,
                    PageNumber = chunks[i].PageNumber,
                    Content = chunks[i].Content,
                    TokenCount = chunks[i].TokenCount,
                    Embedding = vectors[i],
                    EmbeddingModel = settings.Tag
                });
            }

            document.PageCount = parsed.PageCount;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);

            log.LogInformation("Document {DocumentId} ({FileName}) saved with {Count} embedded chunks",
                document.Id, document.FileName, chunks.Count);
        }

        private async Task<Vector[]> EmbedAsync(
            Document document, IReadOnlyList<TextChunk> chunks, EmbeddingOptions settings, CancellationToken ct)
        {
            // On a reindex, chunks whose text is unchanged keep their vector (same model and size only).
            var existing = await db.DocumentChunks
                .Where(c => c.DocumentId == document.Id && c.Embedding != null && c.EmbeddingModel == settings.Tag)
                .Select(c => new { c.Content, c.Embedding })
                .ToListAsync(ct);

            var reusable = new Dictionary<string, Vector>();
            foreach (var chunk in existing) reusable.TryAdd(chunk.Content, chunk.Embedding!);

            var result = new Vector[chunks.Count];
            var missing = new List<int>();
            for (var i = 0; i < chunks.Count; i++)
            {
                if (reusable.TryGetValue(chunks[i].Content, out var vector)) result[i] = vector;
                else missing.Add(i);
            }

            if (missing.Count > 0)
            {
                var generated = await embeddings.GenerateAsync(
                    missing.Select(i => chunks[i].Content).ToList(),
                    new EmbeddingGenerationOptions
                    {
                        ModelId = settings.Model,
                        Dimensions = settings.Dimensions,
                        AdditionalProperties = new AdditionalPropertiesDictionary
                        {
                            [EmbeddingTaskTypes.Key] = EmbeddingTaskTypes.Document
                        }
                    },
                    ct);

                if (generated.Count != missing.Count)
                    throw new InvalidOperationException(
                        $"Embedding provider returned {generated.Count} vectors for {missing.Count} chunks.");

                for (var j = 0; j < missing.Count; j++)
                {
                    var values = generated[j].Vector.ToArray();
                    if (values.Length != settings.Dimensions)
                        throw new InvalidOperationException(
                            $"Embedding has {values.Length} dimensions but the database column is {settings.Dimensions}.");
                    result[missing[j]] = new Vector(values);
                }
            }

            log.LogInformation("Embeddings for {DocumentId}: {Reused} reused, {Embedded} generated",
                document.Id, chunks.Count - missing.Count, missing.Count);

            return result;
        }
    }
}
