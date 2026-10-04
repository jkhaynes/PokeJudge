namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Chunking;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;
using PokeJudge.Tests.TestDoubles;

public class RerankingRetrieverTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string id, double cosine) =>
        new(new EmbeddedChunk(new TextChunk(id, id, $"Text for {id}", Source), new[] { 1f }), cosine);

    private static readonly IReadOnlyList<ScoredChunk> FourCandidates = new[]
    {
        Chunk("a", 0.90), Chunk("b", 0.85), Chunk("c", 0.80), Chunk("d", 0.75)
    };

    [Fact]
    public async Task RetrieveAsync_AsksTheInnerRetrieverForCandidateCount()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.1, 0.2, 0.3, 0.4);

        await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 2);

        Assert.Equal(new[] { 30 }, inner.TopKValues);
    }

    [Fact]
    public async Task RetrieveAsync_ScoresTheQueryTextAgainstEveryCandidate()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.1, 0.2, 0.3, 0.4);

        await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("deck under 60", topK: 2);

        Assert.Equal("deck under 60", scorer.Situations.Single());
        Assert.Same(FourCandidates, scorer.Candidates.Single());
    }

    [Fact]
    public async Task RetrieveAsync_ReturnsTopKByRelevance()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.10, 0.70, 0.05, 0.95);

        var results = await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 2);

        Assert.Equal(new[] { "d", "b" }, results.Select(r => r.Chunk.Chunk.ChunkId));
    }

    [Fact]
    public async Task RetrieveAsync_TiesKeepCosineOrder()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.5, 0.5, 0.5, 0.9);

        var results = await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 3);

        Assert.Equal(new[] { "d", "a", "b" }, results.Select(r => r.Chunk.Chunk.ChunkId));
    }

    [Fact]
    public async Task RetrieveAsync_KeepsTheCosineScore()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.1, 0.2, 0.3, 0.99);

        var results = await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 1);

        Assert.Equal(0.75, results.Single().Score);
    }

    [Fact]
    public async Task RetrieveAsync_ReportsEveryCandidateWithItsCosineRank()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.1, 0.2, 0.3, 0.99);
        IReadOnlyList<RerankedCandidate>? reported = null;

        await new RerankingRetriever(inner, scorer, candidateCount: 30, onReranked: r => reported = r).RetrieveAsync("query", topK: 1);

        Assert.Equal(4, reported!.Count);
        Assert.Equal(("d", 0.99, 4), (reported[0].Chunk.Chunk.Chunk.ChunkId, reported[0].Relevance, reported[0].CosineRank));
    }

    [Fact]
    public async Task RetrieveAsync_ScoreCountMismatch_Throws()
    {
        var inner = new StubRetriever();
        inner.Enqueue(FourCandidates);
        var scorer = new StubRelevanceScorer();
        scorer.Enqueue(0.1, 0.2);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 2));

        Assert.Contains("2 score(s) for 4 candidate(s)", ex.Message);
    }

    [Fact]
    public async Task RetrieveAsync_NoCandidates_SkipsTheScorer()
    {
        var inner = new StubRetriever();
        inner.Enqueue(Array.Empty<ScoredChunk>());
        var scorer = new StubRelevanceScorer();

        var results = await new RerankingRetriever(inner, scorer, candidateCount: 30).RetrieveAsync("query", topK: 5);

        Assert.Empty(results);
        Assert.Empty(scorer.Situations);
    }

    [Fact]
    public async Task RetrieveAsync_TopKAboveCandidateCount_Throws()
    {
        var retriever = new RerankingRetriever(new StubRetriever(), new StubRelevanceScorer(), candidateCount: 3);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => retriever.RetrieveAsync("query", topK: 5));
    }

    [Fact]
    public void Constructor_CandidateCountBelowOne_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new RerankingRetriever(new StubRetriever(), new StubRelevanceScorer(), candidateCount: 0));
    }
}
