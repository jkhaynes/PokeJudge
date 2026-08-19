namespace PokeJudge.Evaluation;

// The Milestone 8.5 source-coverage investigation's recorded conclusions, one per
// scenario investigated -- the structured counterpart to
// .project-plans/milestone-8.5/source-coverage-analysis.md's write-up. Populated by
// manually inspecting each scenario's actually-retrieved chunks against the real
// corpus (via `dotnet run -- search`) before this file was written, not guessed at.
public static class SourceCoverageFindings
{
    public static IReadOnlyList<SourceCoverageFinding> All { get; } = new[]
    {
        new SourceCoverageFinding(
            "spectator-badges",
            SourceCoverageLevel.Sufficient,
            "PPTRH-2.4#0 explicitly states a badge must be worn 'during the entirety of your participation in " +
            "or attendance at the event' -- 'attendance' covers spectators, not just competitors. The material " +
            "is present; the recurring failure to reach sufficiency is a gap in the sufficiency-assessment " +
            "step's willingness to commit to this inferential connection, not a retrieval or corpus problem."),

        new SourceCoverageFinding(
            "drew-extra-card",
            SourceCoverageLevel.Sufficient,
            "PPG-5.5.1 explicitly names 'a competitor draws an extra card' as both a Major-error worked example " +
            "and a de-escalation example. The material is unambiguously present; the scenario's repeated " +
            "insufficient-with-zero-questions crash is the known sufficiency-assessment bug, not a source gap."),

        new SourceCoverageFinding(
            "deck-not-shuffled",
            SourceCoverageLevel.Sufficient,
            "TCGTH-6.2, TCGTH-6.2.1, and TCGTH-6.2.2 explicitly and thoroughly cover randomization requirements " +
            "and the Judge-involvement procedure when a deck's randomization is disputed. The material is " +
            "present; this scenario's 3-different-outcomes non-determinism is a reasoning/consistency problem, " +
            "not a retrieval or corpus problem."),

        new SourceCoverageFinding(
            "missed-prize",
            SourceCoverageLevel.PossibleSourceGap,
            "PPG-5.5.1 explicitly covers 'takes a Prize card without Knocking Out a Pokemon' and 'takes too " +
            "many Prize cards after Knocking Out a Pokemon,' but no passage found (via multiple targeted " +
            "searches) explicitly names the exact inverse: forgetting to take a Prize card actually earned. " +
            "This is a genuinely different classification from the other three scenarios above, and a plausible " +
            "partial explanation for why this scenario's crash is so consistently reproducible: with no clear " +
            "passage to hang a clarifying question on, the sufficiency-assessment step may have less to work " +
            "with here than in the Sufficient-coverage cases.",
            MissingInformation: "An explicit worked example or rule covering a competitor forgetting to take a " +
                "Prize card they were entitled to, analogous to how PPG-5.5.1 already covers the reverse case.",
            LikelySourceDocument: "Penalty Guidelines (PPG), likely adjacent to PPG-5.5.1's existing Prize-related examples."),
    };
}
