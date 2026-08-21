namespace PokeJudge.Reliability;

using PokeJudge.AI;
using PokeJudge.Clarification;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

// Milestone 9's new signal, deliberately its own step rather than a field added onto
// RulingResult alongside SourceSupport -- the same reasoning Milestone 6 already
// established for keeping ruling generation and sufficiency assessment separate
// calls. A self-assessment of an already-produced ruling is a genuinely different
// task from producing the ruling, and keeping it a distinct call (mirroring
// GroundingValidator's shape) makes the two signals -- Source Support and
// self-reported confidence -- produced independently enough to meaningfully
// compare, not two fields of one generation pass.
public sealed class ConfidenceEstimator
{
    private readonly ILlmClient _llmClient;

    public ConfidenceEstimator(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    public Task<ConfidenceEstimate> EstimateAsync(
        string scenarioDescription, GameState state, IReadOnlyList<ScoredChunk> retrievedChunks, RulingResult ruling) =>
        _llmClient.CompleteStructuredAsync<ConfidenceEstimate>(
            SystemPrompts.ConfidenceEstimation,
            PromptBuilder.BuildConfidenceEstimationPrompt(scenarioDescription, state, retrievedChunks, ruling),
            ConfidenceEstimateSchema.Schema);
}
