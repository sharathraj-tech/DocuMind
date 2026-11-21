using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocuMind.Application.Ingestion;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Parsing
{
    public sealed class DocxParser:IDocumentParser
    {
        public IReadOnlyCollection<string> Extensions { get; } = [".docx"];

        public async Task<ParsedDocument> ParseAsync(Stream content, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);
            buffer.Position = 0;

            using var doc = WordprocessingDocument.Open(buffer, false);
            var body = doc.MainDocumentPart?.Document?.Body
                       ?? throw new InvalidOperationException("The DOCX file has no body.");

            var acc = new SectionAccumulator();
            foreach (var element in body.Elements())
            {
                switch (element)
                {
                    case Paragraph p:
                        if (HeadingLevel(p) is { } level) acc.StartSection(level, p.InnerText);
                        else acc.Append(p.InnerText);
                        break;

                    case Table table:
                        foreach (var row in table.Elements<TableRow>())
                            acc.Append(string.Join(" | ", row.Elements<TableCell>().Select(c => c.InnerText.Trim())),
                                       sameBlock: true);
                        acc.Append(string.Empty);
                        break;
                }
            }

            return new ParsedDocument(acc.Build());
        }

        private static int? HeadingLevel(Paragraph p)
        {
            var style = p.ParagraphProperties?.ParagraphStyleId?.Val?.Value;
            if (style is null) return null;

            if (style.Equals("Title", StringComparison.OrdinalIgnoreCase)) return 1;
            if (style.StartsWith("Heading", StringComparison.OrdinalIgnoreCase)
                && int.TryParse(style["Heading".Length..], out var level))
                return level;

            return null;
        }
    }
}
