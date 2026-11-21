using DocuMind.Application.Search;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DocuMind.Application.Chat
{
    public static class PromptBuilder
    {
        private static readonly Regex ClosingTags = new(@"</(document|context)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex UnsafeNameChars = new(@"[<>""]", RegexOptions.Compiled);

        // Takes hits in rank order and stops before the budget is exceeded. The best hit is always kept.
        public static IReadOnlyList<SearchHit> SelectWithinBudget(
            IReadOnlyList<SearchHit> hits, int budgetTokens, Func<string, int> countTokens)
        {
            var selected = new List<SearchHit>();
            var used = 0;

            foreach (var hit in hits)
            {
                var tokens = countTokens(hit.Content);
                if (selected.Count > 0 && used + tokens > budgetTokens) break;
                selected.Add(hit);
                used += tokens;
            }

            return selected;
        }

        // Documents are numbered 1..n in the order given, and those numbers are what the model cites.
        public static string BuildContext(IReadOnlyList<SearchHit> hits)
        {
            var sb = new StringBuilder("<context>\n");

            for (var i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                var name = UnsafeNameChars.Replace(hit.DocumentName, "'");
                var page = hit.PageNumber is { } p ? $" page=\"{p}\"" : "";

                // A document must not be able to close our tags and "escape" into the instructions.
                var text = ClosingTags.Replace(hit.Content, @"<\/$1");

                sb.Append($"<document index=\"{i + 1}\" name=\"{name}\"{page}>\n{text}\n</document>\n");
            }

            return sb.Append("</context>").ToString();
        }
    }
}
