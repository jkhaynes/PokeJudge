namespace PokeJudge.Tests.Clarification;

using PokeJudge.Chunking;
using PokeJudge.Clarification;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;
using PokeJudge.Tests.TestDoubles;

public class RulingGeneratorTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string chunkId, string text, double score) =>
        new(new EmbeddedChunk(new TextChunk(chunkId, chunkId, text, Source), new float[] { 1f }), score);

    [Fact]
    public async Task GenerateAsync_ReturnsWhateverTheLlmClientProduces()
    {
        var expected = new RulingResult(
            "Issue a Warning.",
            "Explanation text.",
            new List<string> { "Correct the Prize count." },
            "Procedural Error -- Warning.",
            new List<string> { "A1#0" },
            SourceSupport.Strong,
            "Directly addressed by the retrieved passage.");

        var llm = new StubLlmClient();
        llm.Enqueue(expected);
        var generator = new RulingGenerator(llm);

        var state = new GameState();
        state.AddConfirmedFacts(new[] { "A Pokemon was Knocked Out." });

        var result = await generator.GenerateAsync(
            "A test scenario description.", state, new[] { Chunk("A1#0", "Passage text.", 0.8) });

        Assert.Same(expected, result);
    }

    [Fact]
    public async Task GenerateAsync_SendsScenarioFactsAndRetrievedChunksToTheModel()
    {
        var llm = new StubLlmClient();
        llm.Enqueue(new RulingResult(
            "Recommendation.", "Explanation.", new List<string>(), null,
            new List<string>(), SourceSupport.Insufficient, "No supporting material was retrieved."));
        var generator = new RulingGenerator(llm);

        var state = new GameState();
        state.AddConfirmedFacts(new[] { "A Pokemon was Knocked Out." });

        await generator.GenerateAsync(
            "A test scenario description.", state, new[] { Chunk("A1#0", "Passage text.", 0.8) });

        Assert.Single(llm.UserContents);
        Assert.Contains("A test scenario description.", llm.UserContents[0]);
        Assert.Contains("A Pokemon was Knocked Out.", llm.UserContents[0]);
        Assert.Contains("A1#0", llm.UserContents[0]);
        Assert.Contains("Passage text.", llm.UserContents[0]);
    }
}
