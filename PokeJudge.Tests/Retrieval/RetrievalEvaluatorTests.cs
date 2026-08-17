namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Chunking;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;

public class RetrievalEvaluatorTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Result(string chunkId, string sectionId, double score) =>
        new(new EmbeddedChunk(new TextChunk(chunkId, sectionId, "text", Source), new float[] { 0f }), score);

    [Fact]
    public void Evaluate_ExpectedSectionIsFirstResult_HitAtRankOne()
    {
        var evalCase = new RetrievalEvalCase("What happens on a missed prize?", "PPTRH-1");
        var results = new List<ScoredChunk>
        {
            Result("PPTRH-1#0", "PPTRH-1", 0.9),
            Result("PPTRH-2#0", "PPTRH-2", 0.5),
        };

        var result = RetrievalEvaluator.Evaluate(evalCase, results);

        Assert.True(result.Hit);
        Assert.Equal(1, result.Rank);
    }

    [Fact]
    public void Evaluate_ExpectedSectionIsThirdResult_HitAtRankThree()
    {
        var evalCase = new RetrievalEvalCase("query", "PPTRH-3");
        var results = new List<ScoredChunk>
        {
            Result("PPTRH-1#0", "PPTRH-1", 0.9),
            Result("PPTRH-2#0", "PPTRH-2", 0.7),
            Result("PPTRH-3#0", "PPTRH-3", 0.5),
        };

        var result = RetrievalEvaluator.Evaluate(evalCase, results);

        Assert.True(result.Hit);
        Assert.Equal(3, result.Rank);
    }

    [Fact]
    public void Evaluate_ExpectedSectionNotInResults_Miss()
    {
        var evalCase = new RetrievalEvalCase("query", "PPTRH-9");
        var results = new List<ScoredChunk>
        {
            Result("PPTRH-1#0", "PPTRH-1", 0.9),
            Result("PPTRH-2#0", "PPTRH-2", 0.5),
        };

        var result = RetrievalEvaluator.Evaluate(evalCase, results);

        Assert.False(result.Hit);
        Assert.Null(result.Rank);
    }

    [Fact]
    public void Evaluate_MultipleChunksFromExpectedSection_ReportsEarliestRank()
    {
        var evalCase = new RetrievalEvalCase("query", "PPTRH-3");
        var results = new List<ScoredChunk>
        {
            Result("PPTRH-1#0", "PPTRH-1", 0.9),
            Result("PPTRH-3#0", "PPTRH-3", 0.8),
            Result("PPTRH-3#1", "PPTRH-3", 0.6),
        };

        var result = RetrievalEvaluator.Evaluate(evalCase, results);

        Assert.True(result.Hit);
        Assert.Equal(2, result.Rank);
    }

    [Fact]
    public void Evaluate_EmptyResults_Miss()
    {
        var evalCase = new RetrievalEvalCase("query", "PPTRH-1");

        var result = RetrievalEvaluator.Evaluate(evalCase, new List<ScoredChunk>());

        Assert.False(result.Hit);
        Assert.Null(result.Rank);
    }
}
