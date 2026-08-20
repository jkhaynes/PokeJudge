namespace PokeJudge.Clarification;

using PokeJudge.AI;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

// Milestone 6's rebuilt multi-turn loop: retrieve -> assess -> clarify -> re-retrieve
// (PRD SS11), replacing Milestone 2's fixed mock corpus with real retrieval. Every
// turn builds a retrieval query from the scenario description plus facts confirmed
// so far, retrieves the current top-K chunks, and assesses sufficiency against
// those -- not a static corpus. All state lives in GameState, owned by the app; the
// model is re-supplied the full accumulated context (and freshly retrieved
// passages) on every call.
public sealed class ClarificationLoop
{
    // Single source of truth for top-K, so the loop's per-turn retrieval and
    // Program.cs's final retrieval before ruling generation can't silently
    // drift apart (the same reasoning as Milestone 5's shared embedding-client
    // constructor -- see .project-plans/milestone-5/pr-review.md).
    public const int DefaultTopK = 5;

    private readonly ILlmClient _llmClient;
    private readonly IRetriever _retriever;
    private readonly int _maxTurns;
    private readonly int _topK;

    public ClarificationLoop(ILlmClient llmClient, IRetriever retriever, int maxTurns = 4, int topK = DefaultTopK)
    {
        if (maxTurns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTurns), maxTurns, "maxTurns must be at least 1.");
        }

        _llmClient = llmClient;
        _retriever = retriever;
        _maxTurns = maxTurns;
        _topK = topK;
    }

    public async Task<ClarificationOutcome> RunAsync(
        string scenarioDescription,
        Func<ClarifyingQuestion, Task<string>> askJudge,
        Action<ClarificationResult, IReadOnlyList<ScoredChunk>>? onAssessment = null)
    {
        var state = new GameState();

        for (var turn = 1; turn <= _maxTurns; turn++)
        {
            var query = RetrievalQueryBuilder.Build(scenarioDescription, state.ConfirmedFacts);
            var retrievedChunks = await _retriever.RetrieveAsync(query, _topK);

            var result = await AssessAsync(scenarioDescription, retrievedChunks, state);
            onAssessment?.Invoke(result, retrievedChunks);

            if (result.IsSufficient)
            {
                return ClarificationOutcome.SufficientAt(state, turn);
            }

            if (result.Questions.Count == 0)
            {
                var rationale = string.IsNullOrWhiteSpace(result.Rationale) ? "(none given)" : result.Rationale;
                throw new InsufficientWithoutQuestionsException(
                    "Model reported the scenario insufficient but supplied no clarifying questions. " +
                    $"Model's rationale: \"{rationale}\"");
            }

            foreach (var question in result.Questions)
            {
                var answer = await askJudge(question);
                var extraction = await ExtractFactsAsync(question, answer);

                state.AddConfirmedFacts(extraction.ConfirmedFacts);
                state.AddHypotheses(extraction.Hypotheses);
            }
        }

        return ClarificationOutcome.TurnCapExhausted(state, _maxTurns);
    }

    private Task<ClarificationResult> AssessAsync(
        string scenarioDescription, IReadOnlyList<ScoredChunk> retrievedChunks, GameState state) =>
        _llmClient.CompleteStructuredAsync<ClarificationResult>(
            SystemPrompts.Judge,
            PromptBuilder.BuildSufficiencyPrompt(scenarioDescription, retrievedChunks, state),
            ClarificationResultSchema.Schema);

    private Task<FactExtractionResult> ExtractFactsAsync(ClarifyingQuestion question, string answer) =>
        _llmClient.CompleteStructuredAsync<FactExtractionResult>(
            SystemPrompts.FactExtraction,
            PromptBuilder.BuildFactExtractionPrompt(question, answer),
            FactExtractionResultSchema.Schema);
}
