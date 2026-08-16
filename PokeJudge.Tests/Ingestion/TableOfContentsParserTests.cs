namespace PokeJudge.Tests.Ingestion;

using PokeJudge.Ingestion;

public class TableOfContentsParserTests
{
    [Fact]
    public void Parse_DotLeaderFormat_ExtractsSectionIdHeadingAndPageNumber()
    {
        const string toc = """
            1 Introduction & Using This Handbook ..........................................................................................4
            1.1 Supporting Materials ........................................................................................................4
            2 Participation Fundamentals .....................................................................................................6
            """;

        var entries = TableOfContentsParser.Parse(toc);

        Assert.Equal(3, entries.Count);
        Assert.Equal(new TocEntry("1", "Introduction & Using This Handbook", 4), entries[0]);
        Assert.Equal(new TocEntry("1.1", "Supporting Materials", 4), entries[1]);
        Assert.Equal(new TocEntry("2", "Participation Fundamentals", 6), entries[2]);
    }

    [Fact]
    public void Parse_DeeplyNestedSectionNumber_Parses()
    {
        const string toc = "2.3.1 Publishing Deck/Team Lists....................................................................................9";

        var entries = TableOfContentsParser.Parse(toc);

        Assert.Equal(new TocEntry("2.3.1", "Publishing Deck/Team Lists", 9), Assert.Single(entries));
    }

    [Fact]
    public void Parse_NonNumberedEntry_IsSkippedNotThrown()
    {
        // Real, observed limitation: an "Appendix A: Rating Zones ... 47" style TOC
        // line has no leading section number and won't be recognized as a section.
        // This is intentional (documented) pattern-based-not-semantic behavior, not
        // a bug to silently patch here.
        const string toc = """
            1 Introduction ..........................................................................................4
            Appendix A: Rating Zones ............................................................................................................. 47
            """;

        var entries = TableOfContentsParser.Parse(toc);

        var entry = Assert.Single(entries);
        Assert.Equal("1", entry.SectionId);
    }

    [Fact]
    public void Parse_BlankLines_AreIgnored()
    {
        const string toc = "\n\n1 Introduction ..........................................................................................4\n\n";

        var entries = TableOfContentsParser.Parse(toc);

        Assert.Single(entries);
    }

    [Fact]
    public void Parse_EmptyText_ReturnsEmptyList()
    {
        var entries = TableOfContentsParser.Parse(string.Empty);

        Assert.Empty(entries);
    }
}
