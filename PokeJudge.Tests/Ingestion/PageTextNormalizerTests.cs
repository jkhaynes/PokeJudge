namespace PokeJudge.Tests.Ingestion;

using PokeJudge.Ingestion;

public class PageTextNormalizerTests
{
    [Fact]
    public void StripPageNumberFooter_LeadingLineMatchesExpectedPageNumber_Strips()
    {
        var result = PageTextNormalizer.StripPageNumberFooter(" 15 \nShould a spectator disrupt.", 15);

        Assert.Equal("Should a spectator disrupt.", result);
    }

    [Fact]
    public void StripPageNumberFooter_NoLeadingNumberLine_LeavesTextUnchanged()
    {
        const string text = "Should a spectator disrupt.";

        var result = PageTextNormalizer.StripPageNumberFooter(text, 15);

        Assert.Equal(text, result);
    }

    [Fact]
    public void StripPageNumberFooter_LeadingLineIsADifferentNumber_LeavesTextUnchanged()
    {
        // Guards against accidentally stripping real content that happens to start
        // with a number that isn't actually this page's footer.
        const string text = " 7 \nBody text here.";

        var result = PageTextNormalizer.StripPageNumberFooter(text, 15);

        Assert.Equal(text, result);
    }

    [Fact]
    public void StripPageNumberHeader_LeadingLineEndsInExpectedPageNumber_Strips()
    {
        var result = PageTextNormalizer.StripPageNumberHeader(
            "THE POKEMON TRADING CARD GAME: WEB RULEBOOK | 2026 15\nBody text here.", 15);

        Assert.Equal("Body text here.", result);
    }

    [Fact]
    public void StripPageNumberHeader_LeadingLineEndsInADifferentNumber_LeavesTextUnchanged()
    {
        const string text = "THE POKEMON TRADING CARD GAME: WEB RULEBOOK | 2026 7\nBody text here.";

        var result = PageTextNormalizer.StripPageNumberHeader(text, 15);

        Assert.Equal(text, result);
    }

    [Fact]
    public void StripPageNumberHeader_NoLeadingHeaderLine_LeavesTextUnchanged()
    {
        const string text = "Body text here.";

        var result = PageTextNormalizer.StripPageNumberHeader(text, 15);

        Assert.Equal(text, result);
    }

    [Fact]
    public void CollapseWhitespace_RunsOfSpaces_CollapseToSingleSpace()
    {
        var result = PageTextNormalizer.CollapseWhitespace("Hello   world");

        Assert.Equal("Hello world", result);
    }

    [Fact]
    public void CollapseWhitespace_ManyBlankLines_CollapseToOneParagraphBreak()
    {
        var result = PageTextNormalizer.CollapseWhitespace("Line one\n\n\n\nLine two");

        Assert.Equal("Line one\n\nLine two", result);
    }

    [Fact]
    public void CollapseWhitespace_ManyBlankLinesWithCarriageReturns_CollapseToOneParagraphBreak()
    {
        // Real PDF extraction (PdfPig's ContentOrderTextExtractor) produces \r\n line
        // endings, not bare \n -- confirmed directly in real ingestion output. A
        // fixture using only \n would pass even if this normalization silently did
        // nothing on real input, so this test uses \r\n specifically.
        var result = PageTextNormalizer.CollapseWhitespace("Line one\r\n\r\n\r\n\r\nLine two");

        Assert.Equal("Line one\n\nLine two", result);
    }

    [Fact]
    public void CollapseWhitespace_LeadingAndTrailingWhitespace_Trimmed()
    {
        var result = PageTextNormalizer.CollapseWhitespace("  Leading and trailing   ");

        Assert.Equal("Leading and trailing", result);
    }

    [Fact]
    public void RejoinHyphenatedLineWraps_HyphenAtLineEnd_JoinsAcrossLineBreak()
    {
        var result = PageTextNormalizer.RejoinHyphenatedLineWraps("This is avail-\nable now.");

        Assert.Equal("This is available now.", result);
    }

    [Fact]
    public void RejoinHyphenatedLineWraps_HyphenAtLineEndWithCarriageReturn_JoinsAcrossLineBreak()
    {
        // Real PDF extraction produces \r\n, not bare \n (see the CollapseWhitespace
        // \r\n test above, added after a real bug on exactly this point) -- this
        // makes explicit that the hyphen-rejoin regex's `\r?\n` already covers it.
        var result = PageTextNormalizer.RejoinHyphenatedLineWraps("This is avail-\r\nable now.");

        Assert.Equal("This is available now.", result);
    }

    [Fact]
    public void RejoinHyphenatedLineWraps_NoLineWrapHyphen_LeavesTextUnchanged()
    {
        const string text = "No hyphen here.\nNext line.";

        var result = PageTextNormalizer.RejoinHyphenatedLineWraps(text);

        Assert.Equal(text, result);
    }

    [Fact]
    public void RejoinHyphenatedLineWraps_MidLineHyphen_LeavesTextUnchanged()
    {
        // Only a hyphen immediately followed by a line break is treated as a
        // wrap artifact -- a hyphen in the middle of a line is left alone.
        const string text = "A well-known fact.";

        var result = PageTextNormalizer.RejoinHyphenatedLineWraps(text);

        Assert.Equal(text, result);
    }
}
