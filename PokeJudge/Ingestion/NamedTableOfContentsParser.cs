namespace PokeJudge.Ingestion;

using System.Text.RegularExpressions;

public sealed record NamedTocEntry(string Heading, int PageNumber);

// Counterpart to TableOfContentsParser for documents whose headings aren't numbered
// (e.g. "Special Conditions", not "5.1 Procedural Error") -- observed for real in the
// Pokemon TCG Rulebook, which TableOfContentsParser's numbered-only pattern cannot
// section at all. Every dot-leader TOC line is captured verbatim as a heading,
// including one that happens to start with a digit as part of its own wording (e.g.
// "3 Card Types" meaning "three card types") -- there is no section-number concept to
// separate out here, unlike the numbered parser.
public static class NamedTableOfContentsParser
{
    private static readonly Regex EntryPattern = new(
        @"^(?<heading>.+?)\s*\.{2,}\s*(?<page>\d+)\s*$",
        RegexOptions.Compiled);

    public static IReadOnlyList<NamedTocEntry> Parse(string tocText)
    {
        var entries = new List<NamedTocEntry>();
        var pendingPrefix = string.Empty;

        foreach (var rawLine in tocText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.Equals("Contents", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var match = EntryPattern.Match(line);
            if (!match.Success)
            {
                // A heading too long for one line wraps onto the next in the TOC itself,
                // with no dot leaders/page number on this first line (e.g. "Appendix 18:
                // Rare Fossil, Unidentified Fossil, and" / "Antique Fossil Cards ... 33").
                // Carry it forward and prepend it once the entry's real dot-leader line
                // is found, rather than treating the tail fragment as the whole heading.
                pendingPrefix = pendingPrefix.Length == 0 ? line : $"{pendingPrefix} {line}";
                continue;
            }

            var heading = match.Groups["heading"].Value.Trim();
            var fullHeading = pendingPrefix.Length == 0 ? heading : $"{pendingPrefix} {heading}";
            pendingPrefix = string.Empty;

            entries.Add(new NamedTocEntry(fullHeading, int.Parse(match.Groups["page"].Value)));
        }

        return entries;
    }
}
