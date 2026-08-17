namespace PokeJudge.Ingestion;

using System.Text.RegularExpressions;

// Counterpart to SectionSplitter for documents with unnumbered headings (see
// NamedTableOfContentsParser). Line-start anchoring alone (SectionSplitter's only
// disambiguator) isn't a strong enough filter here: a named heading like "Special
// Conditions" is also ordinary vocabulary reused elsewhere in the same document's
// prose, so this additionally constrains each match to fall within a small window
// of the TOC's own stated page number -- data already available per-page, before
// pages are joined into one body string, so no new extraction step is required.
public static class NamedHeadingSectionSplitter
{
    public static List<IngestedSection> Split(
        IReadOnlyList<(string Text, int PageNumber)> normalizedPages,
        IReadOnlyList<NamedTocEntry> tocEntries,
        SourceDocumentMetadata source,
        string documentCode,
        int pageTolerance = 1)
    {
        var sections = new List<IngestedSection>();
        if (tocEntries.Count == 0)
        {
            return sections;
        }

        var (bodyText, pageBreaks) = JoinWithPageBreaks(normalizedPages);

        // Each heading is located independently, rather than assuming TOC declaration
        // order matches body/extraction order -- real documents can violate that. The
        // Pokemon TCG Rulebook's appendix pages use a layout where PDF text extraction
        // produced "Appendix 7" before "Appendix 6" even though the TOC lists them
        // numerically (a multi-column layout artifact). Matches are sorted by where
        // they actually landed before slicing section boundaries between them, so
        // slicing always operates on two real, correctly-ordered positions regardless
        // of what order the TOC declared them in.
        var found = new List<(NamedTocEntry Entry, int Index, int Length)>();
        foreach (var entry in tocEntries)
        {
            var match = FindHeadingOnExpectedPage(bodyText, pageBreaks, entry, pageTolerance);

            if (match is null)
            {
                throw new InvalidOperationException(
                    $"Could not locate heading \"{entry.Heading}\" within {pageTolerance} page(s) of its expected page {entry.PageNumber}.");
            }

            found.Add((entry, match.Index, match.Length));
        }

        found.Sort((a, b) => a.Index.CompareTo(b.Index));

        var usedSectionIds = new Dictionary<string, int>();
        for (var i = 0; i < found.Count; i++)
        {
            var (entry, index, length) = found[i];
            var textStart = index + length;
            var textEnd = i + 1 < found.Count ? found[i + 1].Index : bodyText.Length;
            var text = bodyText[textStart..textEnd].Trim();

            var sectionId = $"{documentCode}-{UniqueSlug(entry.Heading, usedSectionIds)}";
            sections.Add(new IngestedSection(sectionId, entry.Heading, text, source));
        }

        return sections;
    }

    private static Match? FindHeadingOnExpectedPage(
        string bodyText, IReadOnlyList<(int StartOffset, int PageNumber)> pageBreaks,
        NamedTocEntry entry, int pageTolerance)
    {
        var regex = new Regex(HeadingPattern.Build(entry.Heading), RegexOptions.Multiline);
        var match = regex.Match(bodyText);

        while (match.Success)
        {
            var actualPage = PageAt(pageBreaks, match.Index);
            if (Math.Abs(actualPage - entry.PageNumber) <= pageTolerance)
            {
                return match;
            }

            match = match.NextMatch();
        }

        return null;
    }

    private static int PageAt(IReadOnlyList<(int StartOffset, int PageNumber)> pageBreaks, int offset)
    {
        var page = pageBreaks[0].PageNumber;
        foreach (var (startOffset, pageNumber) in pageBreaks)
        {
            if (startOffset > offset)
            {
                break;
            }

            page = pageNumber;
        }

        return page;
    }

    private static (string BodyText, List<(int StartOffset, int PageNumber)> PageBreaks) JoinWithPageBreaks(
        IReadOnlyList<(string Text, int PageNumber)> pages)
    {
        var sb = new System.Text.StringBuilder();
        var breaks = new List<(int StartOffset, int PageNumber)>();

        foreach (var (text, pageNumber) in pages)
        {
            breaks.Add((sb.Length, pageNumber));
            sb.Append(text);
            sb.Append('\n');
        }

        return (sb.ToString(), breaks);
    }

    private static string UniqueSlug(string heading, Dictionary<string, int> usedSectionIds)
    {
        var baseSlug = Slugify(heading);

        if (!usedSectionIds.TryGetValue(baseSlug, out var count))
        {
            usedSectionIds[baseSlug] = 1;
            return baseSlug;
        }

        usedSectionIds[baseSlug] = count + 1;
        return $"{baseSlug}-{count + 1}";
    }

    private static string Slugify(string heading)
    {
        var lowered = heading.ToLowerInvariant();
        var slug = Regex.Replace(lowered, @"[^a-z0-9]+", "-").Trim('-');
        return slug.Length == 0 ? "section" : slug;
    }
}
