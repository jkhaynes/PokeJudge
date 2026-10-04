namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Chunking;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;

public class SectionRankFinderTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string chunkId) =>
        new(new EmbeddedChunk(new TextChunk(chunkId, chunkId.Split('#')[0], $"Text for {chunkId}", Source), new[] { 1f }), 0.5);

    private static IReadOnlyList<ScoredChunk> Results(int count, params (int Rank, string ChunkId)[] placed)
    {
        var results = Enumerable.Range(1, count).Select(i => Chunk($"FILLER-{i}#0")).ToList();
        foreach (var (rank, chunkId) in placed)
        {
            results[rank - 1] = Chunk(chunkId);
        }

        return results;
    }

    [Fact]
    public void Find_SectionInTopK_IsInTopK()
    {
        var ranks = SectionRankFinder.Find(new[] { "PPG-5.5.1" }, Results(30, (3, "PPG-5.5.1#2")), topK: 5);

        Assert.Equal(new SectionRank("PPG-5.5.1", 3, SectionReach.InTopK), ranks.Single());
    }

    [Fact]
    public void Find_SectionBelowTopKButInResults_IsRerankable()
    {
        var ranks = SectionRankFinder.Find(new[] { "TCGRULES-deck-building" }, Results(30, (17, "TCGRULES-deck-building#0")), topK: 5);

        Assert.Equal(new SectionRank("TCGRULES-deck-building", 17, SectionReach.Rerankable), ranks.Single());
    }

    [Fact]
    public void Find_SectionAbsent_IsOutOfReach()
    {
        var ranks = SectionRankFinder.Find(new[] { "PPG-4.1.1" }, Results(30), topK: 5);

        Assert.Equal(new SectionRank("PPG-4.1.1", null, SectionReach.OutOfReach), ranks.Single());
    }

    [Fact]
    public void Find_SectionAppearsTwice_ReportsTheBestRank()
    {
        var ranks = SectionRankFinder.Find(new[] { "PPG-5.6.1" }, Results(30, (4, "PPG-5.6.1#3"), (9, "PPG-5.6.1#2")), topK: 5);

        Assert.Equal(4, ranks.Single().Rank);
    }

    [Fact]
    public void Find_KeepsTheOrderOfTheRequestedSections()
    {
        var ranks = SectionRankFinder.Find(new[] { "B", "A" }, Results(30, (1, "A#0"), (2, "B#0")), topK: 5);

        Assert.Equal(new[] { "B", "A" }, ranks.Select(r => r.SectionId));
    }
}
