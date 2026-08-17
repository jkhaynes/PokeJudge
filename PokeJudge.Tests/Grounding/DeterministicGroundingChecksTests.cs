namespace PokeJudge.Tests.Grounding;

using PokeJudge.Chunking;
using PokeJudge.Grounding;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;

public class DeterministicGroundingChecksTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string chunkId, double score = 0.8) =>
        new(new EmbeddedChunk(new TextChunk(chunkId, chunkId, $"Text for {chunkId}", Source), new float[] { 1f }), score);

    [Fact]
    public void RetrievalNonEmpty_EmptyChunkList_ReturnsFalse()
    {
        Assert.False(DeterministicGroundingChecks.RetrievalNonEmpty(Array.Empty<ScoredChunk>()));
    }

    [Fact]
    public void RetrievalNonEmpty_NonEmptyChunkList_ReturnsTrue()
    {
        Assert.True(DeterministicGroundingChecks.RetrievalNonEmpty(new[] { Chunk("A1#0") }));
    }

    [Fact]
    public void AllCitationsExist_NoCitations_ReturnsFalse()
    {
        // A ruling that cites nothing has no evidentiary support to check --
        // this must not vacuously pass.
        var retrieved = new[] { Chunk("A1#0") };

        Assert.False(DeterministicGroundingChecks.AllCitationsExist(Array.Empty<string>(), retrieved));
    }

    [Fact]
    public void AllCitationsExist_AllCitedIdsWereRetrieved_ReturnsTrue()
    {
        var retrieved = new[] { Chunk("A1#0"), Chunk("A2#0") };

        Assert.True(DeterministicGroundingChecks.AllCitationsExist(new[] { "A1#0", "A2#0" }, retrieved));
    }

    [Fact]
    public void AllCitationsExist_ACitedIdWasNotRetrieved_ReturnsFalse()
    {
        var retrieved = new[] { Chunk("A1#0") };

        Assert.False(DeterministicGroundingChecks.AllCitationsExist(new[] { "A1#0", "Z9#0" }, retrieved));
    }

    [Fact]
    public void FactsWereSufficient_True_ReturnsTrue()
    {
        Assert.True(DeterministicGroundingChecks.FactsWereSufficient(true));
    }

    [Fact]
    public void FactsWereSufficient_False_ReturnsFalse()
    {
        Assert.False(DeterministicGroundingChecks.FactsWereSufficient(false));
    }
}
