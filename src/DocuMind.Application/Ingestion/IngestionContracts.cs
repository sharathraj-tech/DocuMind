using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Application.Ingestion
{
    public sealed record ParsedSection(int? PageNumber, string? Heading, string Text);

    public sealed record ParsedDocument(IReadOnlyList<ParsedSection> Sections, int? PageCount = null);

    public sealed record TextChunk(int Index, int? PageNumber, string Content, int TokenCount);

    public interface IDocumentParser
    {
        IReadOnlyCollection<string> Extensions { get; }
        Task<ParsedDocument> ParseAsync(Stream content, CancellationToken ct);
    }
    
    public interface IChunker
    {
        IReadOnlyList<TextChunk> Chunk(ParsedDocument document);
    }

    public interface ITokenCounter
    {
        int Count(string text);
    }

}
