namespace PokeJudge.Evaluation;

using PokeJudge.Clarification;
using PokeJudge.Grounding;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

// Drives the real pipeline for one hand-authored scenario -- the same
// ClarificationLoop -> RulingGenerator -> GroundingValidator sequence Program.cs's
// default console flow runs, just with a scripted judge instead of console input.
// No new AI mechanism is introduced here; this is orchestration and trajectory
// capture over what Milestones 6-7 already built.
public sealed class ScenarioEvalRunner
{
    private readonly ClarificationLoop _loop;
    private readonly RulingGenerator _rulingGenerator;
    private readonly GroundingValidator _groundingValidator;

    public ScenarioEvalRunner(ClarificationLoop loop, RulingGenerator rulingGenerator, GroundingValidator groundingValidator)
    {
        _loop = loop;
        _rulingGenerator = rulingGenerator;
        _groundingValidator = groundingValidator;
    }

    public async Task<ScenarioTrajectory> RunAsync(EvalScenario scenario)
    {
        var turns = new List<TurnRecord>();
        var scriptedAnswerUsed = false;
        var askedMoreQuestionsThanScripted = false;
        IReadOnlyList<ScoredChunk>? lastRetrievedChunks = null;

        ClarificationOutcome outcome;
        try
        {
            outcome = await _loop.RunAsync(
                scenario.InitialDescription,
                askJudge: _ =>
                {
                    if (!scriptedAnswerUsed && scenario.ScriptedAnswer is not null)
                    {
                        scriptedAnswerUsed = true;
                        return Task.FromResult(scenario.ScriptedAnswer);
                    }

                    // Milestone 8's branching scenarios script exactly one answer (PRD SS15's
                    // own single-branch-point example) -- a loop that asks for more is a real,
                    // informative outcome to record and score, not a reason to crash the harness.
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

        return ScenarioTrajectory.Completed(scenario, turns, outcome.TurnsUsed, askedMoreQuestionsThanScripted, ruling, grounding);
    }
}
