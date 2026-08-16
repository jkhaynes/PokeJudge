namespace PokeJudge.Ingestion;

// Orchestrates extract -> normalize -> parse TOC -> split into sections, given
// already-extracted page texts. Deliberately decoupled from PdfTextExtractor (the
// actual PDF/file-system boundary) so this orchestration logic is unit-testable
// with small fixture strings instead of a real multi-page PDF -- the same pattern
// Milestone 2's ClarificationLoop used with an injected ILlmClient instead of a
// live network call.
public sealed class IngestionPipeline
{
    public IngestedDocument Run(
        IReadOnlyList<string> rawPageTexts,
        int titlePageNumber,
        (int Start, int End) tocPageRange,
        (int Start, int End) bodyPageRange,
        string documentTitle,
        string documentCode)
    {
        var titlePageText = rawPageTexts[titlePageNumber - 1];
        var effectiveDate = DocumentMetadataParser.ExtractEffectiveDate(titlePageText);
        var metadata = new SourceDocumentMetadata(documentTitle, effectiveDate, null);

        var tocText = string.Join("\n", PagesInRange(rawPageTexts, tocPageRange));
        var tocEntries = TableOfContentsParser.Parse(tocText);

        var normalizedBodyPages = new List<string>();
        for (var pageNumber = bodyPageRange.Start; pageNumber <= bodyPageRange.End; pageNumber++)
        {
            var raw = rawPageTexts[pageNumber - 1];
            var footerStripped = PageTextNormalizer.StripPageNumberFooter(raw, pageNumber);
            var rejoined = PageTextNormalizer.RejoinHyphenatedLineWraps(footerStripped);
            var normalized = PageTextNormalizer.CollapseWhitespace(rejoined);
            normalizedBodyPages.Add(normalized);
        }

        var bodyText = string.Join("\n", normalizedBodyPages);
        var sections = SectionSplitter.Split(bodyText, tocEntries, metadata, documentCode);

        return new IngestedDocument(metadata, sections);
    }

    private static IEnumerable<string> PagesInRange(IReadOnlyList<string> rawPageTexts, (int Start, int End) range)
    {
        for (var pageNumber = range.Start; pageNumber <= range.End; pageNumber++)
        {
            yield return rawPageTexts[pageNumber - 1];
        }
    }
}
