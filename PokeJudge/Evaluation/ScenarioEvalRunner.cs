namespace PokeJudge.Evaluation;

using PokeJudge.Clarification;
using PokeJudge.Grounding;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

// Drives the real pipeline for one hand-authored scenario -- the same
// ClarificationLoop -> RulingGenerator -> GroundingValidator sequence Program.cs's
// default console flow runs, just with a simulated judge instead of console input.
// No new AI mechanism is introduced here; this is orchestration and trajectory
// capture over what Milestones 6-7 already built.
public sealed class ScenarioEvalRunner
{
    private readonly ClarificationLoop _loop;
    private readonly RulingGenerator _rulingGenerator;
    private readonly GroundingValidator _groundingValidator;
    private readonly SimulatedJudge _judge;

    public ScenarioEvalRunner(
        ClarificationLoop loop, RulingGenerator rulingGenerator, GroundingValidator groundingValidator, SimulatedJudge judge)
    {
        _loop = loop;
        _rulingGenerator = rulingGenerator;
        _groundingValidator = groundingValidator;
        _judge = judge;
    }

    public async Task<ScenarioTrajectory> RunAsync(EvalScenario scenario)
    {
        var turns = new List<TurnRecord>();
        var exchanges = new List<JudgeExchange>();
        IReadOnlyList<ScoredChunk>? lastRetrievedChunks = null;

        ClarificationOutcome outcome;
        try
        {
            outcome = await _loop.RunAsync(
                scenario.InitialDescription,
                askJudge: async question =>
                {
                    JudgeAnswer reply;
                    try
                    {
                        reply = await _judge.AnswerAsync(scenario.InitialDescription, scenario.FactSheet, question.Question);
                    }
                    catch (Exception ex)
                    {
                        throw new SimulatedJudgeException(ex);
                    }
                    exchanges.Add(new JudgeExchange(question.Question, reply.Answer, reply.Known));
                    return reply.Answer;
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
            return ScenarioTrajectory.Failed(scenario, turns, ex.Message, exchanges);
        }

        if (!outcome.Sufficient)
        {
            return ScenarioTrajectory.TurnCapExhausted(scenario, turns, outcome.TurnsUsed, exchanges);
        }

        var finalChunks = lastRetrievedChunks!;
        var ruling = await _rulingGenerator.GenerateAsync(scenario.InitialDescription, outcome.State, finalChunks);
        var grounding = await _groundingValidator.ValidateAsync(ruling, finalChunks, outcome.Sufficient);

        return ScenarioTrajectory.Completed(scenario, turns, outcome.TurnsUsed, ruling, grounding, exchanges);
    }
}
