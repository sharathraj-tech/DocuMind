using DocuMind.Application.Ingestion;
using System;
using System.Collections.Generic;
using System.Text;

namespace DocuMind.Infrastructure.Parsing
{
    internal sealed class SectionAccumulator
    {
        private readonly List<ParsedSection> _sections = [];
        private readonly List<(int Level, string Text)> _headings = [];
        private readonly StringBuilder _text = new();

        public void StartSection(int level, string heading)
        {
            Flush();

            heading = heading.Trim();
            if (heading.Length == 0) return;

            _headings.RemoveAll(h => h.Level >= level);
            _headings.Add((level, heading));
        }

        public void Append(string? text, bool sameBlock = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (_text.Length > 0) _text.Append(sameBlock ? "\n" : "\n\n");
            _text.Append(text.Trim());
        }

        public IReadOnlyList<ParsedSection> Build()
        {
            Flush();
            return _sections;
        }

        private void Flush()
        {
            if (_text.Length == 0) return;

            var heading = _headings.Count == 0 ? null : string.Join(" > ", _headings.Select(h => h.Text));
            _sections.Add(new ParsedSection(null, heading, _text.ToString()));
            _text.Clear();
        }
    }
}
