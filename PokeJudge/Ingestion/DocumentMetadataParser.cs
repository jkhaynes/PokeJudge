namespace PokeJudge.Ingestion;

using System.Text.RegularExpressions;

// Extracts the document's own stated revision date from its title page, rather
// than relying on a manually-typed constant that could go stale. Automatic
// multi-version reconciliation (comparing across document versions) stays out of
// scope -- this only reads what a single document says about itself.
public static class DocumentMetadataParser
{
    private static readonly Regex RevisionDatePattern = new(
        @"LAST REVISION:\s*([A-Za-z]+ \d{1,2},\s*\d{4})",
        RegexOptions.Compiled);

    public static string ExtractEffectiveDate(string titlePageText)
    {
        var match = RevisionDatePattern.Match(titlePageText);
        if (!match.Success)
        {
            throw new InvalidOperationException(
                "Could not find a \"LAST REVISION: <date>\" line on the title page.");
        }

        return match.Groups[1].Value;
    }
}
