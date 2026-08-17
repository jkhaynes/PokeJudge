namespace PokeJudge.Clarification;

using PokeJudge.AI;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

// Ruling generation as its own explicit step (PRD SS11's architecture diagram
// draws this as a separate box from sufficiency/clarification, and FR7 requires
// it to run against a fresh, final retrieval). The caller is responsible for
// re-retrieving against the complete accumulated scenario before calling this --
// this class only builds the prompt and makes the one model call.
public sealed class RulingGenerator
{
    private readonly ILlmClient _llmClient;

    public RulingGenerator(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    public Task<RulingResult> GenerateAsync(
        string scenarioDescription, GameState state, IReadOnlyList<ScoredChunk> retrievedChunks) =>
        _llmClient.CompleteStructuredAsync<RulingResult>(
            SystemPrompts.RulingGeneration,
            PromptBuilder.BuildRulingPrompt(scenarioDescription, state, retrievedChunks),
            RulingResultSchema.Schema);
}
