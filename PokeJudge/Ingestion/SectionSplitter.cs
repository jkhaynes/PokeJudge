namespace PokeJudge.Ingestion;

// Splits normalized body text into citable sections using the TOC entries as
// ground truth for where each section starts. If a TOC entry's heading can't be
// located verbatim in the body text, this fails loudly rather than silently
// producing a gap or a misaligned section -- consistent with the project's
// "errors must fail visibly" requirement (PRD S9).
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

        for (var i = 0; i < tocEntries.Count; i++)
        {
            headingLines[i] = $"{tocEntries[i].SectionId} {tocEntries[i].Heading}";
            var index = bodyText.IndexOf(headingLines[i], StringComparison.Ordinal);

            if (index < 0)
            {
                throw new InvalidOperationException(
                    $"Could not locate heading \"{headingLines[i]}\" in the body text.");
            }

            headingIndexes[i] = index;
        }

        for (var i = 0; i < tocEntries.Count; i++)
        {
            var textStart = headingIndexes[i] + headingLines[i].Length;
            var textEnd = i + 1 < tocEntries.Count ? headingIndexes[i + 1] : bodyText.Length;
            var text = bodyText[textStart..textEnd].Trim();

            sections.Add(new IngestedSection($"{documentCode}-{tocEntries[i].SectionId}", tocEntries[i].Heading, text, source));
        }

        return sections;
    }
}
