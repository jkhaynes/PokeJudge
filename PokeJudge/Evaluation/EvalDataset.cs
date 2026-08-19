namespace PokeJudge.Evaluation;

using PokeJudge.StructuredState;

// Hand-authored per PRD SS15 -- small, not statistically rigorous (Milestone 9's
// required limitations analysis grapples with that directly). Section IDs and
// scripted answers are grounded in real runs from this project's own history
// (Milestones 5-7's observed-limitations.md), not guessed at:
// - "notes", "proxy-cards", "deck-not-shuffled", "spectator-badges" reuse queries
//   and expected sections already verified in Milestone 5's RetrievalEvalSet and
//   Milestone 6's real runs.
// - "repeat-violations" reuses a Milestone 5 eval query, retargeted to PPG-4.2.2 --
//   the corpus grew in Milestone 7's ingestion work, and PPG-4.2.2 ("Repeated
//   Infractions") is now the genuinely better-supported answer.
// - "special-condition" reuses the exact scenario and answer from Milestones 6-7's
//   real runs, including the documented Strong/Partial divergence -- both are
//   accepted here deliberately, per grounding-analysis.md SS4.
// - "missed-prize" reuses the exact scenario that reproducibly failed loudly in
//   Milestones 6 and 7 -- now a regression check for that known behavior.
// - "drew-extra-card" was verified live via `dotnet run -- search` before being
//   added (PPG-5.5.1 explicitly covers this example).
public static class EvalDataset
{
    public static IReadOnlyList<EvalScenario> Scenarios { get; } = new[]
    {
        new EvalScenario(
            "notes",
            "Tournament Procedure",
            "Is a competitor allowed to keep written notes during their match?",
            new[] { "TCGTH-7.4.6" },
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
            ScriptedAnswer: null,
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong }),

        new EvalScenario(
            "proxy-cards",
            "Deck/Decklist Issues",
            "Can a player use proxy cards printed at home during a sanctioned tournament?",
            new[] { "TCGTH-2.4" },
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
            ScriptedAnswer: null,
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong }),

        new EvalScenario(
            "deck-not-shuffled",
            "Illegal Game State",
            "A player's deck wasn't fully shuffled before the game started -- what should happen?",
            new[] { "TCGTH-6.2" },
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
            ScriptedAnswer: null,
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        new EvalScenario(
            "spectator-badges",
            "Tournament Procedure",
            "Do spectators need to wear a badge at large tournaments?",
            new[] { "PPTRH-2.4" },
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
            ScriptedAnswer: null,
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong }),

        new EvalScenario(
            "repeat-violations",
            "Penalty Questions",
            "How are penalties handled for a competitor with a history of repeat violations?",
            new[] { "PPG-4.2.2" },
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
            ScriptedAnswer: null,
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Discretion-required / illegal game state. Both Strong and Partial
        // are accepted for the final label -- Milestone 7's own real run produced a
        // validated Strong where the model's self-report was Partial, and manual review
        // found the model's read arguably more defensible (grounding-analysis.md SS4).
        // Encoding both as acceptable here is itself the honest scoring choice, not a
        // cop-out: this scenario's correct final label is a genuinely open question this
        // project has already found real evidence for.
        new EvalScenario(
            "special-condition",
            "Illegal Game State / Discretion Required",
            "A judge notices during a match that a player's Active Pokemon has a Special Condition marker on " +
            "it that doesn't seem right for what happened. The marker is Asleep, but the player says their " +
            "opponent's attack was supposed to cause Confused, not Asleep.",
            new[] { "TCGRULES-special-conditions" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswer: "The condition marker currently on the Active Pokemon is Asleep, and the opponent's attack was supposed to cause Confused instead.",
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "TCGRULES-special-conditions" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Prize errors / missed game actions. Reproducibly failed loudly (isSufficient:
        // false with zero clarifying questions) in both Milestone 6 and Milestone 7's real
        // runs, even after Milestone 7's corpus expansion improved retrieval quality
        // substantially -- see observed-limitations.md in both milestones. This is now a
        // regression check for that known, real failure mode, not an assumption it's fixed.
        new EvalScenario(
            "missed-prize",
            "Prize Errors",
            "During a League Challenge, a player just noticed they forgot to take a Prize card after knocking " +
            "out their opponent's Pokemon two turns ago.",
            Array.Empty<string>(),
            ExpectedTrajectoryOutcome.ExpectedToFailLoudly,
            ScriptedAnswer: null,
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: null),

        // Drawing too many cards / gameplay error. PPG-5.5.1 explicitly lists
        // "a competitor draws an extra card" as a worked example -- confirmed via
        // `dotnet run -- search` before this scenario was added, not assumed.
        new EvalScenario(
            "drew-extra-card",
            "Gameplay Error (Drawing Too Many Cards)",
            "A player drew an extra card during their draw step and didn't realize it until later in the turn.",
            new[] { "PPG-5.5.1" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswer: "The extra card was not caused by any card effect, and it was not noticed or corrected until several turns later.",
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "PPG-5.5.1" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),
    };
}

