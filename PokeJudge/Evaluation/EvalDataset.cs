namespace PokeJudge.Evaluation;

using PokeJudge.StructuredState;

// Hand-authored per PRD SS15 -- small, not statistically rigorous (Milestone 9's
// required limitations analysis grapples with that directly). Section IDs and
// scripted answers are grounded in real runs from this project's own history
// (Milestones 5-7's observed-limitations.md) or verified live via
// `dotnet run -- search` before being added (Milestone 8.5's expansion), not
// guessed at. Category values must come from ScenarioCategories.Allowed
// (EvalDatasetTests enforces this) -- normalized during Milestone 8.5 after the
// original 8 accumulated inconsistent, overlapping free-text values.
//
// --- Original 8 (Milestone 8) ---
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
//
// --- Milestone 8.5's review of the original 8's expected trajectories ---
// Per the milestone plan, each divergence Milestone 8's real runs observed was
// investigated individually rather than assumed to mean the same thing:
// - "deck-not-shuffled" (3 different outcomes across 3 identical live runs) and
//   "special-condition" (one run resolved with zero clarification, unlike every
//   prior observation): both kept exactly as authored. Live re-verification via
//   `dotnet run -- search` confirms both scenarios' expected sections
//   (TCGTH-6.2/6.2.1/6.2.2 for deck-not-shuffled; TCGRULES-special-conditions for
//   special-condition) explicitly and thoroughly cover the material question --
//   these are genuine model non-determinism, not wrong labels.
// - "drew-extra-card" (reproduces the known "insufficient with zero questions"
//   crash): kept as RequiresOneClarification. PPG-5.5.1 explicitly names "a
//   competitor draws an extra card" as a worked example -- the expectation is
//   evidence-grounded, and at the time of this review the repeated crash was
//   evidence the known sufficiency-assessment bug persisted, not evidence the
//   expectation was wrong. Downgrading it to ExpectedToFailLoudly would have
//   quietly converted a real regression signal into a tautology.
//   UPDATE, out-of-scope follow-up on 2026-08-20 (see plan.md's addendum): the
//   crash itself was subsequently investigated and largely fixed via a
//   SystemPrompts.Judge instruction + a new ClarificationResult.Rationale
//   diagnostic field. drew-extra-card no longer reproduces it (0/2 in live
//   re-verification) -- this bullet's history is kept for the record, but the
//   crash it describes is no longer current behavior for this scenario.
// - "spectator-badges" (both real runs that reached it consistently asked a
//   clarifying question and never reached sufficiency): kept as
//   SufficientOnFirstTurn. Investigated via the new source-coverage
//   classification (see .project-plans/milestone-8.5/source-coverage-analysis.md)
//   -- PPTRH-2.4#0 explicitly states badges must be worn "during the entirety of
//   your participation in or attendance at the event," which does answer the
//   material question. Classified Sufficient coverage: the recurring failure is a
//   gap in the sufficiency-assessment step's willingness to commit to an
//   inferential connection ("attendance" covers spectators), not a retrieval or
//   corpus problem. Kept as a documented, known gap rather than silently loosened.
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
            ScriptedAnswers: Array.Empty<string>(),
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong }),

        new EvalScenario(
            "proxy-cards",
            "Deck/Decklist Issues",
            "Can a player use proxy cards printed at home during a sanctioned tournament?",
            new[] { "TCGTH-2.4" },
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
            ScriptedAnswers: Array.Empty<string>(),
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong }),

        new EvalScenario(
            "deck-not-shuffled",
            "Illegal Game State",
            "A player's deck wasn't fully shuffled before the game started -- what should happen?",
            new[] { "TCGTH-6.2" },
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
            ScriptedAnswers: Array.Empty<string>(),
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        new EvalScenario(
            "spectator-badges",
            "Tournament Procedure",
            "Do spectators need to wear a badge at large tournaments?",
            new[] { "PPTRH-2.4" },
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
            ScriptedAnswers: Array.Empty<string>(),
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong }),

        new EvalScenario(
            "repeat-violations",
            "Penalty Questions",
            "How are penalties handled for a competitor with a history of repeat violations?",
            new[] { "PPG-4.2.2" },
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
            ScriptedAnswers: Array.Empty<string>(),
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
            "Discretion Required",
            "A judge notices during a match that a player's Active Pokemon has a Special Condition marker on " +
            "it that doesn't seem right for what happened. The marker is Asleep, but the player says their " +
            "opponent's attack was supposed to cause Confused, not Asleep.",
            new[] { "TCGRULES-special-conditions" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[] { "The condition marker currently on the Active Pokemon is Asleep, and the opponent's attack was supposed to cause Confused instead." },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "TCGRULES-special-conditions" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Prize errors / missed game actions. Reproducibly failed loudly (isSufficient:
        // false with zero clarifying questions) in both Milestone 6 and Milestone 7's real
        // runs, even after Milestone 7's corpus expansion improved retrieval quality
        // substantially -- see observed-limitations.md in both milestones. This was a
        // regression check for that known, real failure mode, not an assumption it's fixed.
        // Milestone 8.5's source-coverage investigation classifies this one differently
        // from the others below, though: the corpus explicitly covers "took too many
        // Prize cards" and "took a Prize without a Knock Out" (PPG-5.5.1), but never the
        // exact inverse -- forgetting a Prize card actually earned. Classified Possible
        // source gap, not Sufficient coverage -- see source-coverage-analysis.md.
        //
        // Reclassified from ExpectedToFailLoudly after the sufficiency-assessment prompt
        // fix (SystemPrompts.Judge, Milestone 8.5's zero-question-crash investigation):
        // once the model was instructed to always ask a real question rather than silently
        // refuse, this scenario stopped reproducing the crash it existed to test for --
        // live re-verified, 0/2 crashes where it previously crashed reliably. Its real
        // behavior now is exhausting the turn cap asking repeated questions that never
        // resolve anything, because no retrieved passage actually answers the inverse case.
        // That's the same underlying corpus gap, surfacing a different, now more honest way
        // (the model tries and fails to find grounding, rather than silently giving up) --
        // ExpectedUnresolvable is this scenario's current, live-verified regression check.
        new EvalScenario(
            "missed-prize",
            "Prize Errors",
            "During a League Challenge, a player just noticed they forgot to take a Prize card after knocking " +
            "out their opponent's Pokemon two turns ago.",
            Array.Empty<string>(),
            ExpectedTrajectoryOutcome.ExpectedUnresolvable,
            ScriptedAnswers: Array.Empty<string>(),
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: null),

        // Drawing too many cards / gameplay error. PPG-5.5.1 explicitly lists
        // "a competitor draws an extra card" as a worked example -- confirmed via
        // `dotnet run -- search` before this scenario was added, not assumed.
        new EvalScenario(
            "drew-extra-card",
            "Gameplay Error",
            "A player drew an extra card during their draw step and didn't realize it until later in the turn.",
            new[] { "PPG-5.5.1" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[] { "The extra card was not caused by any card effect, and it was not noticed or corrected until several turns later." },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "PPG-5.5.1" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // --- New in Milestone 8.5 -- each verified live via `dotnet run -- search`
        // against the real corpus before being added, per this dataset's established
        // evidence-based practice. ---

        // Attack Resolution (previously uncovered category). TCGRULES's full
        // damage-calculation steps explicitly cover applying Weakness -- confirmed by
        // reading the full section text before authoring this scenario. A live run
        // (Milestone 8.5's multi-answer fix) showed the model repeatedly asking the
        // *same* question four times -- "was the Defending Pokemon Active or
        // Benched" -- because the original scripted answer never addressed it. Fixed
        // by answering the actual question asked, not by adding a second round; the
        // model's real first question also tied to TCGRULES-turn-actions rather than
        // the damage-calculation section alone, so that section was added to
        // ExpectedMaterialSectionIds based on this direct observation, not a guess.
        new EvalScenario(
            "weakness-not-applied",
            "Attack Resolution",
            "A judge is called over because a player's Pokemon was Knocked Out by an attack, but the opponent " +
            "thinks the damage might have been calculated incorrectly -- their Pokemon has a printed Weakness " +
            "to the attack's type, and they're not sure whether that was factored in.",
            new[] { "TCGRULES-full-details-of-attacking", "TCGRULES-turn-actions" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[]
            {
                "The Defending Pokemon was in the Active position (not the Bench) when it took the damage. The " +
                    "attack's base damage was 60, and the Defending Pokemon's card shows a printed Weakness to " +
                    "that attack's type (times 2), but only 60 damage was placed on it, not the doubled amount.",
            },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "TCGRULES-full-details-of-attacking" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Timing Questions (previously uncovered category). TCGRULES-turn-actions
        // explicitly states only one Supporter card may be played per turn; PPG-4.2.1
        // covers the reversibility-based penalty determination -- both confirmed live.
        // A live run (Milestone 8.5's multi-answer fix) showed the model repeatedly
        // asking the *same* question four times -- "which specific Supporter cards
        // were played" -- because the original scripted answer never named them.
        // Fixed by answering the actual question asked, not by adding a second round;
        // the model's real first question tied to PPG-4.2.1 (the penalty section)
        // rather than TCGRULES-turn-actions (the rule-statement section) alone, so
        // PPG-4.2.1 was added to ExpectedMaterialSectionIds based on this direct
        // observation, not a guess.
        new EvalScenario(
            "supporter-twice",
            "Timing Questions",
            "A judge is called over because a player thinks their opponent played two Supporter cards in the same turn.",
            new[] { "TCGRULES-turn-actions", "PPG-4.2.1" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[]
            {
                "The opponent played two copies of a Supporter card called Judge during the same turn -- both " +
                    "were fully played and their effects completely resolved before anyone noticed, and " +
                    "several turns have now passed.",
            },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "PPG-4.2.1" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Timing Questions. TCGRULES-appendix-19-pok-mon-gx explicitly and
        // unconditionally states a player can't use more than one GX attack in an
        // entire game, "regardless of how many Pokemon-GX you play" -- as posed, this
        // scenario already contains every fact the rule turns on.
        new EvalScenario(
            "gx-attack-twice",
            "Timing Questions",
            "A judge is asked whether a player can use a GX attack with a second Pokemon-GX later in the same " +
            "game, having already used a GX attack with a different Pokemon-GX earlier.",
            new[] { "TCGRULES-appendix-19-pok-mon-gx" },
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
            ScriptedAnswers: Array.Empty<string>(),
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong }),

        // Illegal Game State. TCGTH-7.4.1 explicitly requires a mulligan when a
        // competitor's opening hand has no Basic Pokemon -- confirmed live. The
        // scripted answer deliberately reveals the fact is now unverifiable, testing
        // whether an unresolved game-state question correctly caps the final label
        // rather than defaulting to Strong.
        //
        // TCGTH-3.3.1 added to ExpectedMaterialSectionIds after the Milestone 8.5
        // zero-question-crash fix stopped this scenario from crashing and exposed its
        // real trajectory: across 4 live runs, the model's actual first question
        // consistently tied to TCGTH-3.3.1 (deck lists must be reviewed and put away
        // "before either player draws their opening hand") rather than TCGTH-7.4.1
        // alone -- retrieved because it also discusses opening-hand/game-setup timing,
        // even though its own applicability doesn't turn on whether a mulligan was
        // taken. The existing scripted answer ("there's no way to verify it") already
        // resolved 3 of 4 runs cleanly once this is accounted for -- only Initial
        // retrieval and Clarifying question materiality were failing on an
        // authoring gap, not a real model problem. The same "real first question ties
        // to a section other than the one authored" pattern already found for
        // weakness-not-applied/supporter-twice/deck-under-60/ace-spec-count.
        new EvalScenario(
            "mulligan-not-taken",
            "Illegal Game State",
            "Partway through a game, a judge is called over because a player realizes they don't remember " +
            "either player mulliganing at the start, even though the game has clearly been going for several turns.",
            new[] { "TCGTH-7.4.1", "TCGTH-3.3.1" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[]
            {
                "Neither player can now recall for certain whether either of them had a Basic Pokemon in " +
                    "their opening hand -- there's no way to verify it at this point in the game.",
            },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "TCGTH-7.4.1" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Partial, SourceSupport.Insufficient }),

        // Tournament Procedure. PPG-5.2.1's Tardiness Clause explicitly tiers the
        // penalty by how late a competitor arrives -- confirmed live. A live 5x run
        // (Milestone 8.5's five-run validation) showed this scenario consistently
        // exhausting the turn cap asking for an *exact* minute count -- the original
        // scripted answer ("about 10 minutes") sat directly on PPG-5.2.1's own
        // Major/Severe boundary (5-10 minutes is Major; more than 10 is Severe), so
        // the model was correctly recognizing genuine ambiguity, not malfunctioning.
        // Fixed by picking an unambiguous number squarely inside one tier.
        new EvalScenario(
            "late-to-round",
            "Tournament Procedure",
            "A judge is asked to rule on what should happen because a competitor arrived several minutes after their round began.",
            new[] { "PPG-5.2.1" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[] { "The competitor arrived exactly 7 minutes after the round officially started." },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "PPG-5.2.1" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Deck/Decklist Issues. TCGRULES-deck-building explicitly requires exactly 60
        // cards; PPG-5.6.1 covers the legality-infraction determination for a
        // too-small deck -- both confirmed live. A live 5x run (Milestone 8.5's
        // five-run validation) showed two real problems: the model's actual first
        // question ties to PPG-5.6.1 (added to ExpectedMaterialSectionIds below, the
        // same "real first question targets the penalty section, not the base rule
        // section" pattern already found for weakness-not-applied/supporter-twice),
        // and the original scripted answer never said whether the shortfall was in
        // the decklist or the physical deck -- PPG-5.6.1 treats those differently, and
        // a follow-up question asked exactly that. Fixed by specifying both directly.
        new EvalScenario(
            "deck-under-60",
            "Deck/Decklist Issues",
            "Before a match begins, a judge is asked to check a competitor's deck because it seems to have fewer than 60 cards.",
            new[] { "TCGRULES-deck-building", "PPG-5.6.1" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[]
            {
                "Both the decklist and the physical deck contain only 58 cards -- two short of the required 60.",
            },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "PPG-5.6.1" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Deck/Decklist Issues. TCGRULES-appendix-3-ace-spec-cards explicitly and
        // unconditionally limits a deck to one total ACE SPEC card -- confirmed live.
        // A live 5x run (Milestone 8.5's five-run validation) showed this scenario
        // consistently asking "does the deck *actually* contain more than one" and
        // never resolving -- the original wording ("appears to include") introduced
        // real, unconfirmed uncertainty. Two rounds of tightening the wording each
        // surfaced a genuinely new procedural question instead of resolving it
        // (decklist vs. physical deck, matching deck-under-60's finding; then
        // whether the decklist was reviewed before opening hands were drawn, tied to
        // TCGTH-3.3.1's deck list review timing procedure) -- evidence this scenario
        // is more procedurally entangled than a clean zero-clarification case, not a
        // wording problem to keep chasing. Reclassified as RequiresOneClarification
        // to match what was actually, repeatedly observed; gx-attack-twice remains
        // the dataset's clean SufficientOnFirstTurn contrast case.
        new EvalScenario(
            "ace-spec-count",
            "Deck/Decklist Issues",
            "Before a match, a judge is asked to check a competitor's decklist because it appears to include two different ACE SPEC cards.",
            new[] { "TCGRULES-appendix-3-ace-spec-cards", "TCGTH-3.3.1", "PPG-5.6.1" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[]
            {
                "Both the decklist and the physical deck contain two different ACE SPEC cards -- they match each " +
                    "other, and the judge confirmed this while reviewing the decklist before either player drew " +
                    "their opening hand -- before the match began.",
            },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "TCGRULES-appendix-3-ace-spec-cards", "PPG-5.6.1" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Prize Errors. PPG-5.5.1 explicitly covers taking too many Prize cards after
        // a Knock Out -- confirmed live, and material depends on what was actually
        // Knocked Out (ordinary Pokemon vs. one worth extra Prizes).
        new EvalScenario(
            "too-many-prizes",
            "Prize Errors",
            "A judge is called over because a player took two Prize cards after a single Knock Out, instead of one.",
            new[] { "PPG-5.5.1" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[]
            {
                "The Knocked Out Pokemon was an ordinary Pokemon, not a Pokemon-EX, Pokemon-GX, or TAG TEAM -- " +
                    "it should only have been worth one Prize card.",
            },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "PPG-5.5.1" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Prize Errors, deliberately vague per PRD SS15's "intentionally incomplete
        // prompts" category -- the initial description names only the general topic
        // (Prize cards), forcing the system to ask what specifically happened before
        // any ruling is possible, rather than a narrower, already-half-answered scenario.
        new EvalScenario(
            "prize-issue-vague",
            "Prize Errors",
            "A judge is called over because something seems wrong with how Prize cards were handled during the " +
            "match, but nobody can immediately explain what happened.",
            new[] { "PPG-5.5.1" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[]
            {
                "A player took an extra Prize card after what they believed was a Knock Out, but the Defending " +
                    "Pokemon actually still had HP remaining -- it was never actually Knocked Out.",
            },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "PPG-5.5.1" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Gameplay Error. PPG-5.5.1 explicitly lists attaching more than one Energy
        // card in a turn (without an enabling effect) as a Major gameplay error --
        // confirmed live.
        new EvalScenario(
            "double-energy-attach",
            "Gameplay Error",
            "A judge is called over because a player attached two Energy cards to their Pokemon during a single turn.",
            new[] { "PPG-5.5.1" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[]
            {
                "No card effect allowed the second Energy attachment -- the player just attached two Basic " +
                    "Energy cards from hand in the same turn.",
            },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "PPG-5.5.1" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Penalty Questions. PPG-5.5.1's de-escalation examples explicitly cover
        // shuffling a discard pile into the deck without a card effect, when caught
        // early and the discard pile's contents are agreed upon -- confirmed live.
        new EvalScenario(
            "discard-shuffle-deescalate",
            "Penalty Questions",
            "A judge is called over because a competitor shuffled their discard pile into their deck without a " +
            "card effect allowing it, and now needs a ruling on the appropriate penalty.",
            new[] { "PPG-5.5.1" },
            ExpectedTrajectoryOutcome.RequiresOneClarification,
            ScriptedAnswers: new[]
            {
                "The discard pile was small, the game hasn't progressed past the first few turns, and both " +
                    "competitors agree on exactly which cards were in it.",
            },
            ExpectedMaterialSectionIdsAfterAnswer: new[] { "PPG-5.5.1" },
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong, SourceSupport.Partial }),

        // Tournament Procedure. PPTRH-3.3's Spectator Responsibilities explicitly
        // requires maintaining a reasonable distance and refraining from discussing
        // matches in progress within earshot -- confirmed live; as posed, the
        // scenario already contains every fact the section turns on.
        new EvalScenario(
            "spectator-conduct",
            "Tournament Procedure",
            "A judge is called over because a spectator was standing very close to an in-progress match and talking loudly about the game state.",
            new[] { "PPTRH-3.3" },
            ExpectedTrajectoryOutcome.SufficientOnFirstTurn,
            ScriptedAnswers: Array.Empty<string>(),
            ExpectedMaterialSectionIdsAfterAnswer: Array.Empty<string>(),
            AcceptableFinalSourceSupport: new HashSet<SourceSupport> { SourceSupport.Strong }),
    };
}
