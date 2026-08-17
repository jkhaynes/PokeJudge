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

    // Counterpart to Run for documents with unnumbered headings (see
    // NamedTableOfContentsParser/NamedHeadingSectionSplitter) -- kept as a separate
    // method, not a branch inside Run, so the numbered-heading path (relied on by
    // every document ingested so far) stays completely unchanged. Body pages are kept
    // separate with their page numbers, rather than joined into one string up front,
    // because NamedHeadingSectionSplitter needs per-page attribution to disambiguate
    // a named heading from the same phrase reused elsewhere in the document's prose.
    public IngestedDocument RunNamedHeadings(
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

        // Every page of this document type repeats a title/year/page-number header as
        // its own first line (see PageTextNormalizer.StripPageNumberHeader) -- stripped
        // here for the TOC page(s) too, not just body pages, since left in place it gets
        // mistaken by NamedTableOfContentsParser for a heading-wrap continuation line.
        var tocPageTexts = new List<string>();
        for (var pageNumber = tocPageRange.Start; pageNumber <= tocPageRange.End; pageNumber++)
        {
            tocPageTexts.Add(PageTextNormalizer.StripPageNumberHeader(rawPageTexts[pageNumber - 1], pageNumber));
        }

        var tocText = string.Join("\n", tocPageTexts);
        var tocEntries = NamedTableOfContentsParser.Parse(tocText);

        var normalizedBodyPages = new List<(string Text, int PageNumber)>();
        for (var pageNumber = bodyPageRange.Start; pageNumber <= bodyPageRange.End; pageNumber++)
        {
            var raw = rawPageTexts[pageNumber - 1];
            var headerStripped = PageTextNormalizer.StripPageNumberHeader(raw, pageNumber);
            var footerStripped = PageTextNormalizer.StripPageNumberFooter(headerStripped, pageNumber);
            var rejoined = PageTextNormalizer.RejoinHyphenatedLineWraps(footerStripped);
            var normalized = PageTextNormalizer.CollapseWhitespace(rejoined);
            normalizedBodyPages.Add((normalized, pageNumber));
        }

        var sections = NamedHeadingSectionSplitter.Split(normalizedBodyPages, tocEntries, metadata, documentCode);

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
