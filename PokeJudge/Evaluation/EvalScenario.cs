namespace PokeJudge.Evaluation;

using PokeJudge.StructuredState;

// What a hand-authored eval scenario expects the real pipeline to do, per PRD SS15's
// trajectory-evaluation framing -- not just "what's the right final answer," but
// "did the investigation reach it the right way."
public enum ExpectedTrajectoryOutcome
{
    // Resolves without asking a clarifying question -- the initial description is
    // already materially complete.
    SufficientOnFirstTurn,

    // One or more clarifying rounds are expected before sufficiency; ScriptedAnswers
    // supplies the answers the harness gives when asked, one per round, in order.
    RequiresOneClarification,

    // The scenario is expected to reproduce a known, real "fail loudly" case
    // (Milestone 2's insufficient-with-zero-questions guard) -- a scored outcome in
    // its own right, not something to route around.
    ExpectedToFailLoudly
}

// A flat representation, not a nested tree -- PRD SS18 explicitly leaves the exact
// branching-scenario representation open ("flat list vs. a small tree/graph...a
// simple hand-authored representation is sufficient"). Two branches of the same
// underlying decision point (e.g. "Was a Pokemon Knocked Out? Yes / No") are two
// separate EvalScenario rows sharing the same InitialDescription, not one record
// with nested children -- simpler to run (each row is exactly one real pipeline
// execution) and simpler to score.
public sealed record EvalScenario(
    string Id,
    string Category,
    string InitialDescription,
    IReadOnlyList<string> ExpectedMaterialSectionIds,
    ExpectedTrajectoryOutcome ExpectedOutcome,
    IReadOnlyList<string> ScriptedAnswers,
    IReadOnlyList<string> ExpectedMaterialSectionIdsAfterAnswer,
    IReadOnlySet<SourceSupport>? AcceptableFinalSourceSupport);
