using DocuMind.Application.Ingestion;
using System;
using System.Collections.Generic;
using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace DocuMind.Infrastructure.Parsing
{
    public sealed class PdfParser:IDocumentParser
    {
        public IReadOnlyCollection<string> Extensions { get; } = [".pdf"];

        public async Task<ParsedDocument> ParseAsync(Stream content, CancellationToken ct)
        {
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, ct);

            using var pdf = PdfDocument.Open(buffer.ToArray());

            var sections = new List<ParsedSection>();
            foreach (var page in pdf.GetPages())
            {
                ct.ThrowIfCancellationRequested();

                var text = ContentOrderTextExtractor.GetText(page);
                if (!string.IsNullOrWhiteSpace(text))
                    sections.Add(new ParsedSection(page.Number, null, text));
            }

            if (sections.Count == 0)
                throw new InvalidOperationException(
                    "No extractable text found. Scanned PDFs need OCR, which isn't supported yet.");

            return new ParsedDocument(sections, pdf.NumberOfPages);
        }
    }
}
