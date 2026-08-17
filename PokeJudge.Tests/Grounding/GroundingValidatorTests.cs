namespace PokeJudge.Tests.Grounding;

using PokeJudge.Chunking;
using PokeJudge.Grounding;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;
using PokeJudge.Tests.TestDoubles;

public class GroundingValidatorTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string chunkId, string text, double score) =>
        new(new EmbeddedChunk(new TextChunk(chunkId, chunkId, text, Source), new float[] { 1f }), score);

    private static RulingResult SomeRuling(params string[] citedChunkIds) => new(
        "Issue a Warning.",
        "Explanation text.",
        new List<string>(),
        null,
        citedChunkIds.ToList(),
        SourceSupport.Strong,
        "The model's own opinion.");

    [Fact]
    public async Task ValidateAsync_CombinesDeterministicChecksWithTheModelsAssessment()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new GroundingAssessment(
            new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) },
            ConflictDetected: false,
            Rationale: "Directly addressed."));

        var validator = new GroundingValidator(llm);
        var chunks = new[] { Chunk("A1#0", "Passage text.", 0.8) };

        var result = await validator.ValidateAsync(SomeRuling("A1#0"), chunks, clarificationWasSufficient: true);

        Assert.Equal(SourceSupport.Strong, result.ValidatedSourceSupport);
        Assert.True(result.RetrievalNonEmpty);
        Assert.True(result.AllCitationsExist);
        Assert.True(result.FactsWereSufficient);
    }

    [Fact]
    public async Task ValidateAsync_ClarificationWasNotSufficient_ForcesInsufficientRegardlessOfModelAssessment()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new GroundingAssessment(
            new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) },
            ConflictDetected: false,
            Rationale: "Directly addressed."));

        var validator = new GroundingValidator(llm);
        var chunks = new[] { Chunk("A1#0", "Passage text.", 0.8) };

        var result = await validator.ValidateAsync(SomeRuling("A1#0"), chunks, clarificationWasSufficient: false);

        Assert.Equal(SourceSupport.Insufficient, result.ValidatedSourceSupport);
        Assert.False(result.FactsWereSufficient);
    }

    [Fact]
    public async Task ValidateAsync_SendsTheRulingAndCitedChunkTextToTheModel()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new GroundingAssessment(
            new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.ExplicitSupport) },
            ConflictDetected: false,
            Rationale: "Directly addressed."));

        var validator = new GroundingValidator(llm);
        var chunks = new[] { Chunk("A1#0", "Passage text.", 0.8) };

        await validator.ValidateAsync(SomeRuling("A1#0"), chunks, clarificationWasSufficient: true);

        Assert.Single(llm.UserContents);
        Assert.Contains("Issue a Warning.", llm.UserContents[0]);
        Assert.Contains("Explanation text.", llm.UserContents[0]);
        Assert.Contains("A1#0", llm.UserContents[0]);
        Assert.Contains("Passage text.", llm.UserContents[0]);
    }

    [Fact]
    public async Task ValidateAsync_ReturnsTheAssessmentFromTheLlmClient()
    {
        var expected = new GroundingAssessment(
            new List<CitationGroundingCheck> { new("A1#0", CitationSupportLevel.Interpretation) },
            ConflictDetected: true,
            Rationale: "Some interpretation required.");

        var llm = new StubLlmClient();
        llm.Enqueue(expected);

        var validator = new GroundingValidator(llm);
        var chunks = new[] { Chunk("A1#0", "Passage text.", 0.8) };

        var result = await validator.ValidateAsync(SomeRuling("A1#0"), chunks, clarificationWasSufficient: true);

        Assert.Same(expected, result.Assessment);
        Assert.Equal(SourceSupport.Partial, result.ValidatedSourceSupport);
    }
}
