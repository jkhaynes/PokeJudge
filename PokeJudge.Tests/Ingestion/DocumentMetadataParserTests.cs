namespace PokeJudge.Tests.Ingestion;

using PokeJudge.Ingestion;

public class DocumentMetadataParserTests
{
    [Fact]
    public void ExtractEffectiveDate_LastRevisionLinePresent_ExtractsDate()
    {
        const string titlePageText =
            "1 Play! Pokemon Tournament Rules Handbook ENGLISH VERSION LAST REVISION: May 21, 2026";

        var result = DocumentMetadataParser.ExtractEffectiveDate(titlePageText);

        Assert.Equal("May 21, 2026", result);
    }

    [Fact]
    public void ExtractEffectiveDate_LastUpdatedLinePresent_ExtractsDate()
    {
        const string titlePageText =
            "POKEMON TRADING CARD GAME RULES / LAST UPDATED: JULY 2026";

        var result = DocumentMetadataParser.ExtractEffectiveDate(titlePageText);

        Assert.Equal("JULY 2026", result);
    }

    [Fact]
    public void ExtractEffectiveDate_NoRevisionLine_ThrowsRatherThanGuessing()
    {
        const string titlePageText = "1 Some Other Document With No Revision Line";

        Assert.Throws<InvalidOperationException>(() => DocumentMetadataParser.ExtractEffectiveDate(titlePageText));
    }
}
