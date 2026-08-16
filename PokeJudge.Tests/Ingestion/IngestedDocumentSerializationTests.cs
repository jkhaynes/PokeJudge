namespace PokeJudge.Tests.Ingestion;

using System.Text.Json;
using PokeJudge.Ingestion;

public class IngestedDocumentSerializationTests
{
    [Fact]
    public void IngestedDocument_RoundTripsThroughJson()
    {
        var source = new SourceDocumentMetadata("Test Handbook", "May 21, 2026", null);
        var document = new IngestedDocument(
            source,
            new List<IngestedSection>
            {
                new("TEST-1", "Introduction", "Intro body text.", source),
                new("TEST-1.1", "Details", "Details body text.", source),
            });

        var json = JsonSerializer.Serialize(document);
        var roundTripped = JsonSerializer.Deserialize<IngestedDocument>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(document.Metadata, roundTripped!.Metadata);
        Assert.Equal(document.Sections.Count, roundTripped.Sections.Count);

        for (var i = 0; i < document.Sections.Count; i++)
        {
            Assert.Equal(document.Sections[i].SectionId, roundTripped.Sections[i].SectionId);
            Assert.Equal(document.Sections[i].Heading, roundTripped.Sections[i].Heading);
            Assert.Equal(document.Sections[i].Text, roundTripped.Sections[i].Text);
            Assert.Equal(document.Sections[i].Source, roundTripped.Sections[i].Source);
        }
    }
}
