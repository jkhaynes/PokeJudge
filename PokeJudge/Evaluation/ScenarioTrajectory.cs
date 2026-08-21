namespace PokeJudge.Evaluation;

using PokeJudge.Grounding;
using PokeJudge.Reliability;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

// One turn's captured, already-known state -- exactly what ClarificationLoop's
// onAssessment callback already exposes, just recorded instead of printed.
public sealed record TurnRecord(
    IReadOnlyList<ScoredChunk> RetrievedChunks,
    bool IsSufficient,
    IReadOnlyList<ClarifyingQuestion> Questions);

// The full record of one scenario's real run through the pipeline -- everything
// ScenarioEvalScorer needs, and nothing it has to re-derive. Application-owned data
// describing what a live model actually did, not a schema the model produced.
public sealed record ScenarioTrajectory(
    EvalScenario Scenario,
    IReadOnlyList<TurnRecord> Turns,
    bool ReachedSufficiency,
    int TurnsUsed,
    bool AskedMoreQuestionsThanScripted,
    bool ThrewExpectedFailure,
    string? FailureMessage,
    RulingResult? Ruling,
    GroundingResult? Grounding,
    ConfidenceEstimate? Confidence = null)
{
    public static ScenarioTrajectory Failed(EvalScenario scenario, IReadOnlyList<TurnRecord> turns, string message) =>
        new(scenario, turns, ReachedSufficiency: false, TurnsUsed: turns.Count, AskedMoreQuestionsThanScripted: false,
            ThrewExpectedFailure: true, FailureMessage: message, Ruling: null, Grounding: null);

    public static ScenarioTrajectory TurnCapExhausted(
        EvalScenario scenario, IReadOnlyList<TurnRecord> turns, int turnsUsed, bool askedMoreQuestionsThanScripted) =>
        new(scenario, turns, ReachedSufficiency: false, turnsUsed, askedMoreQuestionsThanScripted,
            ThrewExpectedFailure: false, FailureMessage: null, Ruling: null, Grounding: null);

    // confidence defaults to null so call sites that don't care about Milestone 9's signal
    // (existing tests, any future caller that only needs Ruling/Grounding) don't need to supply
    // one -- ScenarioEvalRunner is the one real caller that always has a genuine value to pass.
    public static ScenarioTrajectory Completed(
        EvalScenario scenario, IReadOnlyList<TurnRecord> turns, int turnsUsed, bool askedMoreQuestionsThanScripted,
        RulingResult ruling, GroundingResult grounding, ConfidenceEstimate? confidence = null) =>
        new(scenario, turns, ReachedSufficiency: true, turnsUsed, askedMoreQuestionsThanScripted,
            ThrewExpectedFailure: false, FailureMessage: null, ruling, grounding, confidence);
}
