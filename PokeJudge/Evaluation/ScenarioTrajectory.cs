namespace PokeJudge.Evaluation;

using PokeJudge.Grounding;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

// One turn's captured, already-known state -- exactly what ClarificationLoop's
// onAssessment callback already exposes, just recorded instead of printed.
public sealed record TurnRecord(
    IReadOnlyList<ScoredChunk> RetrievedChunks,
    bool IsSufficient,
    IReadOnlyList<ClarifyingQuestion> Questions);

// One clarifying question and the simulated judge's answer to it.
public sealed record JudgeExchange(string Question, string Answer, bool Known);

// The full record of one scenario's real run through the pipeline -- everything
// ScenarioEvalScorer needs, and nothing it has to re-derive. Application-owned data
// describing what a live model actually did, not a schema the model produced.
public sealed record ScenarioTrajectory(
    EvalScenario Scenario,
    IReadOnlyList<TurnRecord> Turns,
    bool ReachedSufficiency,
    int TurnsUsed,
    IReadOnlyList<JudgeExchange> Exchanges,
    bool ThrewExpectedFailure,
    string? FailureMessage,
    RulingResult? Ruling,
    GroundingResult? Grounding)
{
    // A round is a turn in which PokeJudge judged the facts insufficient and asked questions.
    public int ClarifyingRounds => Turns.Count(t => !t.IsSufficient && t.Questions.Count > 0);

    public int NotKnownCount => Exchanges.Count(e => !e.Known);

    public static ScenarioTrajectory Failed(
        EvalScenario scenario, IReadOnlyList<TurnRecord> turns, string message, IReadOnlyList<JudgeExchange>? exchanges = null) =>
        new(scenario, turns, ReachedSufficiency: false, TurnsUsed: turns.Count, exchanges ?? Array.Empty<JudgeExchange>(),
            ThrewExpectedFailure: true, FailureMessage: message, Ruling: null, Grounding: null);

    public static ScenarioTrajectory TurnCapExhausted(
        EvalScenario scenario, IReadOnlyList<TurnRecord> turns, int turnsUsed, IReadOnlyList<JudgeExchange>? exchanges = null) =>
        new(scenario, turns, ReachedSufficiency: false, turnsUsed, exchanges ?? Array.Empty<JudgeExchange>(),
            ThrewExpectedFailure: false, FailureMessage: null, Ruling: null, Grounding: null);

    public static ScenarioTrajectory Completed(
        EvalScenario scenario, IReadOnlyList<TurnRecord> turns, int turnsUsed,
        RulingResult ruling, GroundingResult grounding, IReadOnlyList<JudgeExchange>? exchanges = null) =>
        new(scenario, turns, ReachedSufficiency: true, turnsUsed, exchanges ?? Array.Empty<JudgeExchange>(),
            ThrewExpectedFailure: false, FailureMessage: null, ruling, grounding);
}
