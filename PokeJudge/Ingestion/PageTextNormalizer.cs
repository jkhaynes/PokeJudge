namespace PokeJudge.Ingestion;

using System.Text.RegularExpressions;

// Deterministic text-cleanup functions, each targeting one concrete artifact
// observed in real PDF extraction (see .project-plans/milestone-3/observed-limitations.md):
// a repeating page-number footer, inconsistent whitespace, and hyphenated
// line-wraps. Normalization is deliberately separate from extraction (PdfTextExtractor)
// and from section detection (SectionSplitter) -- each step has its own failure mode.
public static class PageTextNormalizer
{
    private static readonly Regex HorizontalWhitespaceRun = new(@"[ \t]+", RegexOptions.Compiled);
    private static readonly Regex ExcessBlankLines = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex HyphenatedLineWrap = new(@"(?<=\w)-\r?\n(?=\w)", RegexOptions.Compiled);

    public static string StripPageNumberFooter(string pageText, int expectedPageNumber)
    {
        var newlineIndex = pageText.IndexOf('\n');

        string firstLine;
        string rest;

        if (newlineIndex >= 0)
        {
            firstLine = pageText[..newlineIndex];
            rest = pageText[(newlineIndex + 1)..];
        }
        else
        {
            firstLine = pageText;
            rest = string.Empty;
        }

        if (firstLine.Trim() != expectedPageNumber.ToString())
        {
            return pageText;
        }

        return rest.TrimStart('\n', '\r', ' ', '\t');
    }

    public static string CollapseWhitespace(string text)
    {
        // Normalize line endings first: real PDF extraction produces \r\n, and a
        // run of \r\n pairs never contains 3 consecutive \n characters, so
        // ExcessBlankLines would silently never match without this step.
        var normalizedNewlines = text.Replace("\r\n", "\n").Replace("\r", "\n");
        var collapsedSpaces = HorizontalWhitespaceRun.Replace(normalizedNewlines, " ");
        var collapsedBlankLines = ExcessBlankLines.Replace(collapsedSpaces, "\n\n");

        return collapsedBlankLines.Trim();
    }

    // Heuristic only: a hyphen at a line break is assumed to be a wrap artifact and
    // is removed. This cannot distinguish a genuine wrap ("avail-\nable") from a
    // compound word that happened to wrap at its hyphen ("self-\naware") -- doing
    // that reliably would need a dictionary, which is out of scope here.
    public static string RejoinHyphenatedLineWraps(string text)
    {
        return HyphenatedLineWrap.Replace(text, string.Empty);
    }
}
