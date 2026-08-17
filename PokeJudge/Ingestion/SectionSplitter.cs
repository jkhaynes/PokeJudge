namespace PokeJudge.Ingestion;

using System.Text.RegularExpressions;

// Splits normalized body text into citable sections using the TOC entries as
// ground truth for where each section starts. If a TOC entry's heading can't be
// located in the body text, this fails loudly rather than silently producing a
// gap or a misaligned section -- consistent with the project's "errors must
// fail visibly" requirement (PRD S9).
public static class SectionSplitter
{
    public static List<IngestedSection> Split(
        string bodyText,
        IReadOnlyList<TocEntry> tocEntries,
        SourceDocumentMetadata source,
        string documentCode)
    {
        var sections = new List<IngestedSection>();
        if (tocEntries.Count == 0)
        {
            return sections;
        }

        var headingLines = new string[tocEntries.Count];
        var headingIndexes = new int[tocEntries.Count];
        var headingLengths = new int[tocEntries.Count];

        // A heading is only matched at the start of a line, searching forward from the end
        // of the previous heading rather than from the start of bodyText each time. A
        // short/generic heading (e.g. a one-word subsection title) can coincidentally appear
        // earlier -- or between two real headings -- as an ordinary mid-sentence cross-
        // reference (or, as observed in the real Penalty Guidelines document, get quoted
        // verbatim in a later "Summary of Changes" table); a plain substring search would
        // lock onto that false occurrence and silently misorder headingIndexes, producing a
        // negative-length slice below instead of a clear error. Line-start anchoring rejects
        // mid-sentence false matches; the forward-only search guarantees headings are located
        // in the TOC's declared order, matching the body's actual reading order.
        //
        // Matching also tolerates a heading wrapping across a line break in the body (a long
        // heading like "5 Base Infractions, Recommended Starting Penalties, and Deviations"
        // can text-wrap onto two lines in the source PDF, which RejoinHyphenatedLineWraps does
        // not rejoin since there's no hyphen) by treating each single space in the TOC's
        // heading text as one-or-more whitespace characters, including a newline, in the body.
        var searchFrom = 0;
        for (var i = 0; i < tocEntries.Count; i++)
        {
            headingLines[i] = $"{tocEntries[i].SectionId} {tocEntries[i].Heading}";
            var match = FindHeadingMatch(bodyText, headingLines[i], searchFrom);

            if (match is null)
            {
                throw new InvalidOperationException(
                    $"Could not locate heading \"{headingLines[i]}\" at the start of a line, at or after position {searchFrom}.");
            }

            headingIndexes[i] = match.Index;
            headingLengths[i] = match.Length;
            searchFrom = match.Index + match.Length;
        }

        for (var i = 0; i < tocEntries.Count; i++)
        {
            var textStart = headingIndexes[i] + headingLengths[i];
            var textEnd = i + 1 < tocEntries.Count ? headingIndexes[i + 1] : bodyText.Length;
            var text = bodyText[textStart..textEnd].Trim();

            sections.Add(new IngestedSection($"{documentCode}-{tocEntries[i].SectionId}", tocEntries[i].Heading, text, source));
        }

        return sections;
    }

    private static Match? FindHeadingMatch(string bodyText, string headingLine, int searchFrom)
    {
        var match = new Regex(HeadingPattern.Build(headingLine), RegexOptions.Multiline).Match(bodyText, searchFrom);
        return match.Success ? match : null;
    }
}
