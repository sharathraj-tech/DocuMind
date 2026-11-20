using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace DocuMind.Application.Chat
{
    public static class CitationParser
    {
        // Matches [1], [12], and [1, 2]. Adjacent markers like [2][3] are matched one at a time.
        private static readonly Regex Marker = new(@"\[(\d+(?:\s*,\s*\d+)*)\]", RegexOptions.Compiled);
        private static readonly Regex MarkerWithLeadingSpace = new(@"\s*\[\d+(?:\s*,\s*\d+)*\]", RegexOptions.Compiled);

        // Distinct source numbers the answer cites, ascending. Numbers outside 1..max are ignored.
        public static IReadOnlyList<int> ExtractIndexes(string answer, int max)
        {
            var found = new SortedSet<int>();
            foreach (Match match in Marker.Matches(answer))
            {
                foreach (var part in match.Groups[1].Value.Split(','))
                {
                    if (int.TryParse(part.Trim(), out var n) && n >= 1 && n <= max)
                        found.Add(n);
                }
            }
            return found.ToList();
        }

        // Old answers are replayed to the model as history; their [n] would point at sources that are gone.
        public static string StripMarkers(string text) => MarkerWithLeadingSpace.Replace(text, "");

    }
}
