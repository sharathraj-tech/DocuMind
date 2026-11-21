using DocuMind.Application.Ingestion;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Parsing
{
    public sealed class PlainTextParser:IDocumentParser
    {
        public IReadOnlyCollection<string> Extensions { get; } = [".txt"];

        public async Task<ParsedDocument> ParseAsync(Stream content, CancellationToken ct)
        {
            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(ct);
            return new ParsedDocument([new ParsedSection(null, null, text)]);
        }
    }
}
