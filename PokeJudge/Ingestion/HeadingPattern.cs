namespace PokeJudge.Ingestion;

using System.Text.RegularExpressions;

// Shared by SectionSplitter (numbered headings) and NamedHeadingSectionSplitter
// (unnumbered headings): builds a regex that requires a heading to start at the
// beginning of a line, while tolerating a heading that text-wraps across a line
// break in the body (a long heading can wrap onto two lines in the source PDF
// with no hyphen, so PageTextNormalizer.RejoinHyphenatedLineWraps won't have
// rejoined it -- observed for real ingesting the Penalty Guidelines document).
internal static class HeadingPattern
{
    public static string Build(string headingLine)
    {
        var words = headingLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return "^" + string.Join(@"\s+", words.Select(Regex.Escape));
    }
}
