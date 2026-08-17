namespace PokeJudge.Tests.Clarification;

using PokeJudge.Chunking;
using PokeJudge.Clarification;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

public class PromptBuilderTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static ScoredChunk Chunk(string chunkId, string sectionId, string text, double score) =>
        new(new EmbeddedChunk(new TextChunk(chunkId, sectionId, text, Source), new float[] { 1f }), score);

    private static readonly IReadOnlyList<ScoredChunk> RetrievedChunks = new[]
    {
        Chunk("A1#0", "A1", "Material passage text.", 0.81),
        Chunk("A2#0", "A2", "Irrelevant passage text.", 0.42),
    };

    [Fact]
    public void BuildSufficiencyPrompt_IncludesScenarioDescriptionAndAllRetrievedChunks()
    {
        var prompt = PromptBuilder.BuildSufficiencyPrompt("A test scenario description.", RetrievedChunks, new GameState());

        Assert.Contains("A test scenario description.", prompt);
        Assert.Contains("A1#0", prompt);
        Assert.Contains("Material passage text.", prompt);
        Assert.Contains("A2#0", prompt);
        Assert.Contains("Irrelevant passage text.", prompt);
    }

    [Fact]
    public void BuildSufficiencyPrompt_WithNoFactsYet_StillLabelsBothSections()
    {
        var prompt = PromptBuilder.BuildSufficiencyPrompt("A test scenario description.", RetrievedChunks, new GameState());

        Assert.Contains("Confirmed facts", prompt);
        Assert.Contains("Hypotheses", prompt);
    }

    [Fact]
    public void BuildSufficiencyPrompt_IncludesConfirmedFactsAndHypotheses_WhenPresent()
    {
        var state = new GameState();
        state.AddConfirmedFacts(new[] { "A Pokemon was Knocked Out." });
        state.AddHypotheses(new[] { "The player probably lost track of time." });

        var prompt = PromptBuilder.BuildSufficiencyPrompt("A test scenario description.", RetrievedChunks, state);

        Assert.Contains("A Pokemon was Knocked Out.", prompt);
        Assert.Contains("The player probably lost track of time.", prompt);
    }

    [Fact]
    public void BuildSufficiencyPrompt_NoRetrievedChunks_StillProducesAPrompt()
    {
        var prompt = PromptBuilder.BuildSufficiencyPrompt(
            "A test scenario description.", Array.Empty<ScoredChunk>(), new GameState());

        Assert.Contains("A test scenario description.", prompt);
    }

    [Fact]
    public void BuildFactExtractionPrompt_IncludesQuestionAndAnswer()
    {
        var question = new ClarifyingQuestion("Was a Pokemon Knocked Out?", "A1#0");

        var prompt = PromptBuilder.BuildFactExtractionPrompt(question, "Yes, it was.");

        Assert.Contains("Was a Pokemon Knocked Out?", prompt);
        Assert.Contains("Yes, it was.", prompt);
    }

    [Fact]
    public void BuildRulingPrompt_IncludesScenarioConfirmedFactsAndRetrievedChunks()
    {
        var state = new GameState();
        state.AddConfirmedFacts(new[] { "A Pokemon was Knocked Out." });
        state.AddHypotheses(new[] { "The player probably lost track of time." });

        var prompt = PromptBuilder.BuildRulingPrompt("A test scenario description.", state, RetrievedChunks);

        Assert.Contains("A test scenario description.", prompt);
        Assert.Contains("A Pokemon was Knocked Out.", prompt);
        Assert.Contains("A1#0", prompt);
        Assert.Contains("Material passage text.", prompt);
    }

    [Fact]
    public void BuildRulingPrompt_HypothesesArePresentButLabeledNotUsable()
    {
        var state = new GameState();
        state.AddHypotheses(new[] { "The player probably lost track of time." });

        var prompt = PromptBuilder.BuildRulingPrompt("A test scenario description.", state, RetrievedChunks);

        Assert.Contains("The player probably lost track of time.", prompt);
        Assert.Contains("not confirmed", prompt, StringComparison.OrdinalIgnoreCase);
    }
}
