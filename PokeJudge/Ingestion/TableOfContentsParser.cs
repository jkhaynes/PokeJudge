namespace PokeJudge.Ingestion;

using System.Text.RegularExpressions;

public sealed record TocEntry(string SectionId, string Heading, int PageNumber);

// Parses a real Table of Contents' dot-leader format ("2.3.1 Heading .... 9") into
// structured entries, cross-referenced later (SectionSplitter) against body text --
// a much more robust way to find real section boundaries than pattern-matching
// headings directly in body text, which false-positives on ordinary numbered lists
// (e.g. "1. Visit Pokemon.com." in step-by-step instructions).
//
// Deliberately only recognizes numbered section headings. An "Appendix A: Rating
// Zones ... 47" style entry has no leading section number and is skipped, not
// guessed at -- pattern-based-not-semantic detection missing real-world formatting
// it wasn't designed for, exactly as the milestone plan anticipated.
public static class TableOfContentsParser
{
    private static readonly Regex EntryPattern = new(
        @"^(?<num>\d+(?:\.\d+)*)\s+(?<heading>.+?)\s*\.{2,}\s*(?<page>\d+)\s*$",
        RegexOptions.Compiled);

    public static IReadOnlyList<TocEntry> Parse(string tocText)
    {
        var entries = new List<TocEntry>();

        foreach (var rawLine in tocText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var match = EntryPattern.Match(line);
            if (!match.Success)
            {
                continue;
            }

            entries.Add(new TocEntry(
                match.Groups["num"].Value,
                match.Groups["heading"].Value.Trim(),
                int.Parse(match.Groups["page"].Value)));
        }

        return entries;
    }
}
