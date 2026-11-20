using DocuMind.Application.Common;
using DocuMind.Application.Ingestion;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DocuMind.Infrastructure.Chunking
{
    public sealed class RecursiveChunker(IOptions<ChunkingOptions> options, ITokenCounter tokens) : IChunker
    {
        private static readonly string[] Separators = ["\n\n", "\n", ". ", " "];

        // Postgres text columns reject NUL characters, and Npgsql rejects lone surrogates (both appear in real PDFs).
        private static readonly Regex LoneSurrogates = new(
            @"[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]", RegexOptions.Compiled);
        private static readonly Regex Spaces = new(@"[ \t\u00A0]+", RegexOptions.Compiled);
        private static readonly Regex SpacesAroundNewlines = new(@" ?\n ?", RegexOptions.Compiled);
        private static readonly Regex ManyNewlines = new(@"\n{3,}", RegexOptions.Compiled);

        private readonly ChunkingOptions _o = options.Value;

        public IReadOnlyList<TextChunk> Chunk(ParsedDocument document)
        {
            var sections = document.Sections
                .Select(s => s with { Text = Normalize(s.Text), Heading = s.Heading is null ? null : Normalize(s.Heading) })
                .Where(s => s.Text.Length > 0);

            var result = new List<TextChunk>();
            foreach (var section in MergeSmallSections(sections))
                foreach (var content in ChunkSection(section))
                    result.Add(new TextChunk(result.Count, section.PageNumber, content, tokens.Count(content)));

            return result;
        }

        // Tiny consecutive sections without page numbers (short DOCX/Markdown headings) become one chunk.
        private List<ParsedSection> MergeSmallSections(IEnumerable<ParsedSection> sections)
        {
            var merged = new List<ParsedSection>();
            foreach (var s in sections)
            {
                if (merged.Count > 0
                    && merged[^1] is { PageNumber: null } prev
                    && s.PageNumber is null
                    && tokens.Count(prev.Text) + tokens.Count(s.Text) <= _o.TargetTokens)
                {
                    var label = string.IsNullOrWhiteSpace(s.Heading) ? "" : s.Heading + "\n";
                    merged[^1] = prev with { Text = $"{prev.Text}\n\n{label}{s.Text}" };
                }
                else
                {
                    merged.Add(s);
                }
            }
            return merged;
        }

        private IEnumerable<string> ChunkSection(ParsedSection section)
        {
            var bases = Pack(Split(section.Text, 0));

            for (var i = 0; i < bases.Count; i++)
            {
                var tail = i == 0 ? "" : OverlapTail(bases[i - 1]);
                var body = tail.Length == 0 ? bases[i] : $"{tail} {bases[i]}";

                // The heading path goes on every chunk so each one makes sense on its own.
                yield return string.IsNullOrWhiteSpace(section.Heading) ? body : $"{section.Heading}\n\n{body}";
            }
        }

        private IEnumerable<string> Split(string text, int level)
        {
            if (tokens.Count(text) <= _o.TargetTokens)
            {
                yield return text;
                yield break;
            }

            if (level >= Separators.Length)
            {
                foreach (var part in HardSplit(text)) yield return part;
                yield break;
            }

            var parts = SplitKeepingSeparator(text, Separators[level]);
            if (parts.Count == 1)
            {
                foreach (var p in Split(text, level + 1)) yield return p;
                yield break;
            }

            foreach (var part in parts)
                foreach (var p in Split(part, level + 1))
                    yield return p;
        }

        // Last resort for text with no separators at all (long URLs, minified blobs).
        private IEnumerable<string> HardSplit(string text)
        {
            while (text.Length > 0)
            {
                var total = tokens.Count(text);
                if (total <= _o.TargetTokens)
                {
                    yield return text;
                    yield break;
                }

                var take = Math.Max(1, (int)((long)text.Length * _o.TargetTokens / total));
                if (take > 1 && char.IsHighSurrogate(text[take - 1])) take--;   // don't cut a surrogate pair

                yield return text[..take];
                text = text[take..];
            }
        }

        private List<string> Pack(IEnumerable<string> pieces)
        {
            var chunks = new List<string>();
            var current = new StringBuilder();
            var currentTokens = 0;

            void Flush()
            {
                var text = current.ToString().Trim();
                if (text.Length > 0) chunks.Add(text);
                current.Clear();
                currentTokens = 0;
            }

            foreach (var piece in pieces)
            {
                var t = tokens.Count(piece);
                if (currentTokens + t > _o.TargetTokens && current.Length > 0) Flush();
                current.Append(piece);
                currentTokens += t;
            }
            Flush();

            return chunks;
        }

        private string OverlapTail(string previous)
        {
            var total = tokens.Count(previous);
            if (_o.OverlapTokens <= 0 || total <= _o.OverlapTokens) return "";

            var chars = (int)((long)previous.Length * _o.OverlapTokens / total);
            var start = previous.Length - chars;

            var space = previous.IndexOf(' ', start);   // start the overlap on a word boundary
            if (space >= 0 && space + 1 < previous.Length) start = space + 1;

            return previous[start..];
        }

        // Keeps the separator attached to the piece before it, so joining pieces restores the text.
        private static List<string> SplitKeepingSeparator(string text, string separator)
        {
            var parts = new List<string>();
            var start = 0;

            while (true)
            {
                var index = text.IndexOf(separator, start, StringComparison.Ordinal);
                if (index < 0) break;
                parts.Add(text[start..(index + separator.Length)]);
                start = index + separator.Length;
            }

            if (start < text.Length) parts.Add(text[start..]);
            return parts;
        }

        private static string Normalize(string text)
        {
            text = text.Replace("\0", "").Replace("\r\n", "\n").Replace('\r', '\n');
            text = LoneSurrogates.Replace(text, "");
            text = Spaces.Replace(text, " ");
            text = SpacesAroundNewlines.Replace(text, "\n");
            text = ManyNewlines.Replace(text, "\n\n");
            return text.Trim();
        }
    }
}
