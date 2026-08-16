namespace PokeJudge.Tests.Ingestion;

using PokeJudge.Ingestion;

public class IngestionPipelineTests
{
    [Fact]
    public void Run_SinglePageBody_ProducesMetadataAndSectionsWithFootersStripped()
    {
        // rawPageTexts[0] = page 1 (title), [1] = page 2 (TOC), [2] = page 3 (body)
        var rawPageTexts = new List<string>
        {
            "1 Test Handbook ENGLISH VERSION LAST REVISION: May 21, 2026",
            "1 Introduction ....................2\n1.1 Details ....................2",
            " 3 \n1 Introduction\nIntro body text.\n1.1 Details\nDetails body text.",
        };

        var pipeline = new IngestionPipeline();

        var document = pipeline.Run(
            rawPageTexts,
            titlePageNumber: 1,
            tocPageRange: (2, 2),
            bodyPageRange: (3, 3),
            documentTitle: "Test Handbook",
            documentCode: "TEST");

        Assert.Equal("Test Handbook", document.Metadata.Title);
        Assert.Equal("May 21, 2026", document.Metadata.Version);

        Assert.Equal(2, document.Sections.Count);
        Assert.Equal("TEST-1", document.Sections[0].SectionId);
        Assert.Equal("Intro body text.", document.Sections[0].Text);
        // The " 3 " page-number footer must not leak into the first section's text.
        Assert.DoesNotContain(" 3 ", document.Sections[0].Text);
        Assert.Equal("TEST-1.1", document.Sections[1].SectionId);
        Assert.Equal("Details body text.", document.Sections[1].Text);
    }

    [Fact]
    public void Run_MultiPageBody_StripsEachPagesOwnFooterAndConcatenatesInOrder()
    {
        var rawPageTexts = new List<string>
        {
            "1 Test Handbook ENGLISH VERSION LAST REVISION: May 21, 2026",
            "1 Introduction ....................3\n2 Second Section ....................4",
            " 3 \n1 Introduction\nIntro body text.",
            " 4 \n2 Second Section\nSecond section body text.",
        };

        var pipeline = new IngestionPipeline();

        var document = pipeline.Run(
            rawPageTexts,
            titlePageNumber: 1,
            tocPageRange: (2, 2),
            bodyPageRange: (3, 4),
            documentTitle: "Test Handbook",
            documentCode: "TEST");

        Assert.Equal(2, document.Sections.Count);
        Assert.Equal("Intro body text.", document.Sections[0].Text);
        Assert.Equal("Second section body text.", document.Sections[1].Text);
        Assert.DoesNotContain(" 4 ", document.Sections[0].Text);
    }
}
