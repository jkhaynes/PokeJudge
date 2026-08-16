namespace PokeJudge.Tests.Ingestion;

using PokeJudge.Ingestion;

public class SectionSplitterTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    [Fact]
    public void Split_TwoTocEntries_ProducesTwoSectionsWithCorrectTextBoundaries()
    {
        const string bodyText =
            "1 Introduction\nThis is the introduction body text.\n1.1 Supporting Materials\nThis is supporting materials body text.";

        var tocEntries = new List<TocEntry>
        {
            new("1", "Introduction", 4),
            new("1.1", "Supporting Materials", 4),
        };

        var sections = SectionSplitter.Split(bodyText, tocEntries, Source, "TEST");

        Assert.Equal(2, sections.Count);

        Assert.Equal("TEST-1", sections[0].SectionId);
        Assert.Equal("Introduction", sections[0].Heading);
        Assert.Equal("This is the introduction body text.", sections[0].Text);
        Assert.Equal(Source, sections[0].Source);

        Assert.Equal("TEST-1.1", sections[1].SectionId);
        Assert.Equal("Supporting Materials", sections[1].Heading);
        Assert.Equal("This is supporting materials body text.", sections[1].Text);
    }

    [Fact]
    public void Split_LastEntry_TextRunsToEndOfBody()
    {
        const string bodyText = "1 Introduction\nIntro text.\n1.1 Details\nDetails text runs to the very end.";

        var tocEntries = new List<TocEntry> { new("1", "Introduction", 4), new("1.1", "Details", 4) };

        var sections = SectionSplitter.Split(bodyText, tocEntries, Source, "TEST");

        Assert.Equal("Details text runs to the very end.", sections[1].Text);
    }

    [Fact]
    public void Split_TocEntryHeadingNotFoundInBody_ThrowsNamingTheMissingSection()
    {
        const string bodyText = "1 Introduction\nIntro text.";

        var tocEntries = new List<TocEntry> { new("1", "Introduction", 4), new("9.9", "Nonexistent Section", 99) };

        var ex = Assert.Throws<InvalidOperationException>(
            () => SectionSplitter.Split(bodyText, tocEntries, Source, "TEST"));

        Assert.Contains("9.9", ex.Message);
        Assert.Contains("Nonexistent Section", ex.Message);
    }

    [Fact]
    public void Split_EmptyTocEntries_ReturnsEmptyList()
    {
        var sections = SectionSplitter.Split("Some body text.", new List<TocEntry>(), Source, "TEST");

        Assert.Empty(sections);
    }
}
