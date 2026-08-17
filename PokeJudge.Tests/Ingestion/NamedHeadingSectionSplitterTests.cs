namespace PokeJudge.Tests.Ingestion;

using PokeJudge.Ingestion;

public class NamedHeadingSectionSplitterTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Rulebook", "July 2026", null);

    [Fact]
    public void Split_TwoNamedEntries_ProducesTwoSectionsWithSlugifiedIds()
    {
        var pages = new List<(string Text, int PageNumber)>
        {
            ("Special Conditions\nBody text for conditions.", 15),
            ("Next Heading\nBody text for next.", 16),
        };
        var tocEntries = new List<NamedTocEntry> { new("Special Conditions", 15), new("Next Heading", 16) };

        var sections = NamedHeadingSectionSplitter.Split(pages, tocEntries, Source, "TEST");

        Assert.Equal(2, sections.Count);
        Assert.Equal("TEST-special-conditions", sections[0].SectionId);
        Assert.Equal("Special Conditions", sections[0].Heading);
        Assert.Equal("Body text for conditions.", sections[0].Text);
        Assert.Equal("TEST-next-heading", sections[1].SectionId);
        Assert.Equal("Body text for next.", sections[1].Text);
    }

    [Fact]
    public void Split_HeadingTextReusedOnAnEarlierWrongPage_SkipsItAndMatchesTheExpectedPage()
    {
        // "Bonus Round" sits at the start of a line on both page 5 (an unrelated
        // cross-reference) and page 10 (the real section) -- line-start anchoring alone
        // can't tell these apart; only the TOC's stated page number can.
        var pages = new List<(string Text, int PageNumber)>
        {
            ("Intro text.\nBonus Round\nThis is just a cross-reference mention, not the real section.", 5),
            ("Other stuff.\nBonus Round\nThe real bonus round rules go here.", 10),
        };
        var tocEntries = new List<NamedTocEntry> { new("Bonus Round", 10) };

        var sections = NamedHeadingSectionSplitter.Split(pages, tocEntries, Source, "TEST");

        Assert.Equal("The real bonus round rules go here.", sections[0].Text);
    }

    [Fact]
    public void Split_DuplicateHeadingText_GetsUniqueSectionIds()
    {
        var pages = new List<(string Text, int PageNumber)>
        {
            ("Example\nFirst body.", 3),
            ("Example\nSecond body.", 5),
        };
        var tocEntries = new List<NamedTocEntry> { new("Example", 3), new("Example", 5) };

        var sections = NamedHeadingSectionSplitter.Split(pages, tocEntries, Source, "TEST");

        Assert.Equal("TEST-example", sections[0].SectionId);
        Assert.Equal("TEST-example-2", sections[1].SectionId);
    }

    [Fact]
    public void Split_HeadingNotFoundWithinPageTolerance_ThrowsNamingTheMissingHeading()
    {
        var pages = new List<(string Text, int PageNumber)> { ("Some Page\nBody.", 3) };
        var tocEntries = new List<NamedTocEntry> { new("Nonexistent Heading", 3) };

        var ex = Assert.Throws<InvalidOperationException>(
            () => NamedHeadingSectionSplitter.Split(pages, tocEntries, Source, "TEST"));

        Assert.Contains("Nonexistent Heading", ex.Message);
    }

    [Fact]
    public void Split_TocDeclaresEntriesOutOfBodyOrder_StillProducesCorrectSectionBoundaries()
    {
        // Mirrors the real Pokemon TCG Rulebook: the TOC lists "Appendix 6" before
        // "Appendix 7", but a multi-column page layout caused PDF text extraction to
        // produce "Appendix 7" before "Appendix 6" in the actual page text. Slicing
        // must follow real body position, not TOC declaration order.
        var pages = new List<(string Text, int PageNumber)>
        {
            ("Appendix 7: Lost Zone\nLost zone body text.\nAppendix 6: Tera Pokemon ex\nTera body text.", 26),
        };
        var tocEntries = new List<NamedTocEntry>
        {
            new("Appendix 6: Tera Pokemon ex", 26),
            new("Appendix 7: Lost Zone", 26),
        };

        var sections = NamedHeadingSectionSplitter.Split(pages, tocEntries, Source, "TEST");

        Assert.Equal(2, sections.Count);
        var appendix7 = Assert.Single(sections, s => s.Heading == "Appendix 7: Lost Zone");
        var appendix6 = Assert.Single(sections, s => s.Heading == "Appendix 6: Tera Pokemon ex");
        Assert.Equal("Lost zone body text.", appendix7.Text);
        Assert.Equal("Tera body text.", appendix6.Text);
    }

    [Fact]
    public void Split_EmptyTocEntries_ReturnsEmptyList()
    {
        var pages = new List<(string Text, int PageNumber)> { ("Some text.", 1) };

        var sections = NamedHeadingSectionSplitter.Split(pages, new List<NamedTocEntry>(), Source, "TEST");

        Assert.Empty(sections);
    }
}
