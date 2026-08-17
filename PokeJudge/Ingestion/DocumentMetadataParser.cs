namespace PokeJudge.Ingestion;

using System.Text.RegularExpressions;

// Extracts the document's own stated revision date from its title page, rather
// than relying on a manually-typed constant that could go stale. Automatic
// multi-version reconciliation (comparing across document versions) stays out of
// scope -- this only reads what a single document says about itself.
public static class DocumentMetadataParser
{
    // Most Play! Pokemon documents state "LAST REVISION: <Month> <Day>, <Year>". The Pokemon TCG
    // Rulebook (a different, non-Play!-branded source) instead states "LAST UPDATED: <Month> <Year>"
    // with no day -- both are real, currently-ingested phrasings, not a hypothetical to guard against.
    private static readonly Regex RevisionDatePattern = new(
        @"LAST REVISION:\s*([A-Za-z]+ \d{1,2},\s*\d{4})",
        RegexOptions.Compiled);

    private static readonly Regex UpdatedDatePattern = new(
        @"LAST UPDATED:\s*([A-Za-z]+ \d{4})",
        RegexOptions.Compiled);

    public static string ExtractEffectiveDate(string titlePageText)
    {
        var revisionMatch = RevisionDatePattern.Match(titlePageText);
        if (revisionMatch.Success)
        {
            return revisionMatch.Groups[1].Value;
        }

        var updatedMatch = UpdatedDatePattern.Match(titlePageText);
        if (updatedMatch.Success)
        {
            return updatedMatch.Groups[1].Value;
        }

        throw new InvalidOperationException(
            "Could not find a \"LAST REVISION: <date>\" or \"LAST UPDATED: <date>\" line on the title page.");
    }
}
