namespace PokeJudge.Ingestion;

using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

// The actual PDF/file-system boundary -- not unit tested, same as GeminiLlmClient's
// network boundary in Milestone 2. Deterministic logic downstream of this (in
// IngestionPipeline and friends) is tested against small fixture strings instead.
//
// Uses ContentOrderTextExtractor rather than PdfPig's default page.Text: the
// default blob-of-text extraction does not reliably preserve line breaks (a
// section heading and its following paragraph can run together on one line),
// which breaks section detection. ContentOrderTextExtractor reconstructs reading
// order with real line breaks, which is what makes TOC-driven section splitting
// against body text possible at all.
public static class PdfTextExtractor
{
    public static IReadOnlyList<string> ExtractPageTexts(string filePath)
    {
        using var document = PdfDocument.Open(filePath);

        var pageTexts = new List<string>(document.NumberOfPages);
        for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            pageTexts.Add(ContentOrderTextExtractor.GetText(document.GetPage(pageNumber)));
        }

        return pageTexts;
    }
}
