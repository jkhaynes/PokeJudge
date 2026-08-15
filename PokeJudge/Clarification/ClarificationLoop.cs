namespace PokeJudge.Clarification;

using PokeJudge.AI;
using PokeJudge.StructuredState;

// The multi-turn loop from the milestone plan: assess sufficiency against
// the mock corpus + known facts so far; if insufficient, ask the judge's
// clarifying questions, classify the free-text answers into confirmed facts
// vs. hypotheses, update state, and re-assess -- until sufficient or a
// small turn cap. All state lives in GameState, owned by the app; the model
// is re-supplied the full accumulated context on every call.
public sealed class ClarificationLoop
{
    private readonly ILlmClient _llmClient;
    private readonly int _maxTurns;

    public ClarificationLoop(ILlmClient llmClient, int maxTurns = 4)
    {
        if (maxTurns <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTurns), maxTurns, "maxTurns must be at least 1.");
        }

        _llmClient = llmClient;
        _maxTurns = maxTurns;
    }

    public async Task<ClarificationOutcome> RunAsync(
        MockScenario scenario,
        Func<ClarifyingQuestion, Task<string>> askJudge,
        Action<ClarificationResult>? onAssessment = null)
    {
        var state = new GameState();

        for (var turn = 1; turn <= _maxTurns; turn++)
        {
            var result = await AssessAsync(scenario, state);
            onAssessment?.Invoke(result);

            if (result.IsSufficient)
            {
                if (result.Draft is null)
                {
                    throw new InvalidOperationException(
                        "Model reported the scenario sufficient but did not include a draft ruling.");
                }

                return ClarificationOutcome.SufficientAt(state, result.Draft, turn);
            }

            if (result.Questions.Count == 0)
            {
                throw new InvalidOperationException(
                    "Model reported the scenario insufficient but supplied no clarifying questions.");
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

    private Task<ClarificationResult> AssessAsync(MockScenario scenario, GameState state) =>
        _llmClient.CompleteStructuredAsync<ClarificationResult>(
            SystemPrompts.Judge,
            PromptBuilder.BuildSufficiencyPrompt(scenario, state),
            ClarificationResultSchema.Schema);

    private Task<FactExtractionResult> ExtractFactsAsync(ClarifyingQuestion question, string answer) =>
        _llmClient.CompleteStructuredAsync<FactExtractionResult>(
            SystemPrompts.FactExtraction,
            PromptBuilder.BuildFactExtractionPrompt(question, answer),
            FactExtractionResultSchema.Schema);
}
