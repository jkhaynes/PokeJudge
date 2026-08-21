namespace PokeJudge.Evaluation;

using PokeJudge.Clarification;
using PokeJudge.Grounding;
using PokeJudge.Reliability;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

// Drives the real pipeline for one hand-authored scenario -- the same
// ClarificationLoop -> RulingGenerator -> GroundingValidator -> ConfidenceEstimator
// sequence Program.cs's default console flow runs, just with a scripted judge
// instead of console input. No new AI mechanism beyond Milestone 9's
// ConfidenceEstimator is introduced here; this is orchestration and trajectory
// capture over what Milestones 6-9 already built.
public sealed class ScenarioEvalRunner
{
    private readonly ClarificationLoop _loop;
    private readonly RulingGenerator _rulingGenerator;
    private readonly GroundingValidator _groundingValidator;
    private readonly ConfidenceEstimator _confidenceEstimator;

    public ScenarioEvalRunner(
        ClarificationLoop loop, RulingGenerator rulingGenerator, GroundingValidator groundingValidator,
        ConfidenceEstimator confidenceEstimator)
    {
        _loop = loop;
        _rulingGenerator = rulingGenerator;
        _groundingValidator = groundingValidator;
        _confidenceEstimator = confidenceEstimator;
    }

    public async Task<ScenarioTrajectory> RunAsync(EvalScenario scenario)
    {
        var turns = new List<TurnRecord>();
        var nextScriptedAnswerIndex = 0;
        var askedMoreQuestionsThanScripted = false;
        IReadOnlyList<ScoredChunk>? lastRetrievedChunks = null;

        ClarificationOutcome outcome;
        try
        {
            outcome = await _loop.RunAsync(
                scenario.InitialDescription,
                askJudge: _ =>
                {
                    if (nextScriptedAnswerIndex < scenario.ScriptedAnswers.Count)
                    {
                        var answer = scenario.ScriptedAnswers[nextScriptedAnswerIndex];
                        nextScriptedAnswerIndex++;
                        return Task.FromResult(answer);
                    }

                    // A scenario scripts as many answers as it expects clarifying rounds
                    // (Milestone 8.5: previously exactly one, per PRD SS15's own
                    // single-branch-point example -- now however many rounds the scenario
                    // actually needs). A loop that asks beyond the scripted answers is a
                    // real, informative outcome to record and score, not a reason to crash
                    // the harness.
                    askedMoreQuestionsThanScripted = true;
                    return Task.FromResult(string.Empty);
                },
                onAssessment: (result, chunks) =>
                {
                    lastRetrievedChunks = chunks;
                    turns.Add(new TurnRecord(chunks, result.IsSufficient, result.Questions));
                });
        }
        catch (InsufficientWithoutQuestionsException ex)
        {
            // The exact guard Milestone 2 added for "insufficient with zero questions" --
            // some scenarios are hand-authored specifically to reproduce this known, real
            // failure mode (e.g. the missed-Prize scenario from Milestones 6-7), so this is
            // a scored outcome, not an unhandled error. This is its own exception type,
            // distinct from the generic InvalidOperationException a malformed/null
            // structured response throws elsewhere in the loop (fact extraction, the
            // sufficiency assessment call itself) -- catching only this specific type means
            // an unrelated structured-output failure can't be mistaken for, and silently
            // scored as, this known bug reproducing.
            return ScenarioTrajectory.Failed(scenario, turns, ex.Message);
        }

        if (!outcome.Sufficient)
        {
            return ScenarioTrajectory.TurnCapExhausted(scenario, turns, outcome.TurnsUsed, askedMoreQuestionsThanScripted);
        }

        var finalChunks = lastRetrievedChunks!;
        var ruling = await _rulingGenerator.GenerateAsync(scenario.InitialDescription, outcome.State, finalChunks);
        var grounding = await _groundingValidator.ValidateAsync(ruling, finalChunks, outcome.Sufficient);
        var confidence = await _confidenceEstimator.EstimateAsync(scenario.InitialDescription, outcome.State, finalChunks, ruling);

        return ScenarioTrajectory.Completed(
            scenario, turns, outcome.TurnsUsed, askedMoreQuestionsThanScripted, ruling, grounding, confidence);
    }
}
