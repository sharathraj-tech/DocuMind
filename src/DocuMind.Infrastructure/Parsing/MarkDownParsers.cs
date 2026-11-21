using DocuMind.Application.Ingestion;
using Markdig;
using Markdig.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Parsing
{
    public sealed class MarkDownParsers:IDocumentParser
    {
        public IReadOnlyCollection<string> Extensions { get; } = [".md"];

        public async Task<ParsedDocument> ParseAsync(Stream content, CancellationToken ct)
        {
            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(ct);

            var acc = new SectionAccumulator();
            foreach (var block in Markdown.Parse(text).Descendants<LeafBlock>())
            {
                if (block.Lines.Count == 0) continue;

                var raw = block.Lines.ToString();
                if (block is HeadingBlock heading) acc.StartSection(heading.Level, raw);
                else acc.Append(raw);   
            }

            return new ParsedDocument(acc.Build());
        }
    }
}
