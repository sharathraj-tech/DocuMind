using DocuMind.Application.Ingestion;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DocuMind.Infrastructure.Parsing
{
    public sealed class HtmlParser:IDocumentParser
    {
        private const string BlockXPath =
        ".//h1|.//h2|.//h3|.//h4|.//h5|.//h6|.//p|.//li|.//pre|.//blockquote|.//td|.//th|.//dt|.//dd";

        public IReadOnlyCollection<string> Extensions { get; } = [".html", ".htm"];

        public Task<ParsedDocument> ParseAsync(Stream content, CancellationToken ct)
        {
            var html = new HtmlDocument();
            html.Load(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

            // Drop everything that isn't reading content.
            var noise = html.DocumentNode.SelectNodes("//script|//style|//noscript|//svg|//nav|//footer|//head|//template");
            if (noise is not null)
                foreach (var node in noise) node.Remove();

            var root = html.DocumentNode.SelectSingleNode("//body") ?? html.DocumentNode;
            var acc = new SectionAccumulator();

            var blocks = root.SelectNodes(BlockXPath);
            if (blocks is null)
            {
                acc.Append(Clean(root.InnerText));
                return Task.FromResult(new ParsedDocument(acc.Build()));
            }

            foreach (var node in blocks)
            {
                // <li><p>text</p></li>: take the inner <p> only, so nothing is duplicated.
                if (node.SelectSingleNode(BlockXPath) is not null) continue;

                var text = node.Name == "pre"
                    ? HtmlEntity.DeEntitize(node.InnerText).Trim()
                    : Clean(node.InnerText);

                if (node.Name is ['h', >= '1' and <= '6'])
                    acc.StartSection(node.Name[1] - '0', text);
                else
                    acc.Append(text);
            }

            return Task.FromResult(new ParsedDocument(acc.Build()));
        }

        private static string Clean(string text)
            => Regex.Replace(HtmlEntity.DeEntitize(text), @"\s+", " ").Trim();
    }
}
