namespace PokeJudge.Tests.Ingestion;

using PokeJudge.Ingestion;

public class NamedTableOfContentsParserTests
{
    [Fact]
    public void Parse_DotLeaderLine_ExtractsHeadingAndPage()
    {
        const string tocText = "Special Conditions .......................................................15";

        var entries = NamedTableOfContentsParser.Parse(tocText);

        Assert.Single(entries);
        Assert.Equal("Special Conditions", entries[0].Heading);
        Assert.Equal(15, entries[0].PageNumber);
    }

    [Fact]
    public void Parse_HeadingStartingWithADigit_KeepsTheDigitAsPartOfTheHeadingText()
    {
        // "3 Card Types" means "three card types" here, not "section 3" -- there is
        // no section-number concept for this parser to strip out.
        const string tocText = "3 Card Types ..................................................................6";

        var entries = NamedTableOfContentsParser.Parse(tocText);

        Assert.Equal("3 Card Types", entries[0].Heading);
        Assert.Equal(6, entries[0].PageNumber);
    }

    [Fact]
    public void Parse_MultipleLines_PreservesOrder()
    {
        const string tocText =
            "Become a Pokemon Master! ..................................................3\n" +
            "Energy Types ...............................................................4\n" +
            "Special Conditions .........................................................15";

        var entries = NamedTableOfContentsParser.Parse(tocText);

        Assert.Equal(3, entries.Count);
        Assert.Equal("Become a Pokemon Master!", entries[0].Heading);
        Assert.Equal("Energy Types", entries[1].Heading);
        Assert.Equal("Special Conditions", entries[2].Heading);
    }

    [Fact]
    public void Parse_LineWithNoDotLeaders_IsSkipped()
    {
        const string tocText = "Contents\nSpecial Conditions .........................................................15";

        var entries = NamedTableOfContentsParser.Parse(tocText);

        Assert.Single(entries);
        Assert.Equal("Special Conditions", entries[0].Heading);
    }

    [Fact]
    public void Parse_HeadingWrapsAcrossTwoLinesInTheTocItself_JoinsIntoOneHeading()
    {
        // The real Appendix 18 TOC entry: too long for one line, so the dot leaders and
        // page number only appear on the second line.
        const string tocText =
            "Appendix 18: Rare Fossil, Unidentified Fossil, and\n" +
            "Antique Fossil Cards ........................................33";

        var entries = NamedTableOfContentsParser.Parse(tocText);

        Assert.Single(entries);
        Assert.Equal("Appendix 18: Rare Fossil, Unidentified Fossil, and Antique Fossil Cards", entries[0].Heading);
        Assert.Equal(33, entries[0].PageNumber);
    }

    [Fact]
    public void Parse_ContentsHeaderLine_IsIgnoredRatherThanTreatedAsAWrapPrefix()
    {
        const string tocText = "Contents\nBecome a Pokemon Master! ..................................................3";

        var entries = NamedTableOfContentsParser.Parse(tocText);

        Assert.Single(entries);
        Assert.Equal("Become a Pokemon Master!", entries[0].Heading);
    }
}
