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
    ExpectedToFailLoudly,

    // The scenario is expected to never reach sufficiency within the turn cap,
    // because no retrieved passage actually answers the material question (a
    // genuine corpus gap, not a model bug). Added in Milestone 8.5 alongside the
    // sufficiency-assessment prompt fix that made the model always ask a real
    // question rather than silently refusing (see SystemPrompts.Judge) -- for a
    // scenario like this, that means it now exhausts the turn cap asking
    // questions that never resolve anything, instead of crashing. Distinct from
    // ExpectedToFailLoudly: reaching the turn cap without crashing is the correct,
    // expected outcome here, not a failure to route around.
    ExpectedUnresolvable
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
