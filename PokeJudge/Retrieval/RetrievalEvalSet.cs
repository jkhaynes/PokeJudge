namespace PokeJudge.Retrieval;

// Hand-authored, small (not statistically rigorous -- Milestone 8 formalizes
// that). Each query is phrased in plain judge language, deliberately not
// copying the source document's own wording, to exercise semantic search
// against literal keyword matching. Expected section IDs are grounded in the
// real ingested output for all four documents (PPTRH, TCGTH, PPG, TCGRULES),
// inspected directly before writing these, not guessed at.
//
// Step 4 grew this from 7 to 29 cases so it covers every PPG and TCGRULES section
// an EvalDataset scenario turns on, including the four the 2026-09-27 run never
// retrieved (PPG-5.5.1, TCGRULES-deck-building, TCGRULES-appendix-3-ace-spec-cards
// with PPG-4.1.1, and TCGRULES-turn-actions). The repeat-violations case now expects
// PPG-4.2.2 rather than PPTRH-7.5: EvalDataset retargeted that scenario, and the old
// expectation predates the PPG ingestion.
public static class RetrievalEvalSet
{
    public static IReadOnlyList<RetrievalEvalCase> Cases { get; } = new[]
    {
        // TCG Tournament Handbook and Play! Pokémon Tournament Rules Handbook.
        new RetrievalEvalCase(
            "A player's deck wasn't fully shuffled before the game started -- what should happen?",
            "TCGTH-6.2"),
        new RetrievalEvalCase(
            "Is a competitor allowed to keep written notes during their match?",
            "TCGTH-7.4.6"),
        new RetrievalEvalCase(
            "What happens if a card gets damaged during an event?",
            "TCGTH-2.4"),
        new RetrievalEvalCase(
            "Do spectators need to wear a badge at large tournaments?",
            "PPTRH-2.4"),
        new RetrievalEvalCase(
            "Can a player's match be filmed and shown live to an audience?",
            "PPTRH-4.5"),
        new RetrievalEvalCase(
            "What happens if a player has no Basic Pokemon in their opening hand?",
            "TCGTH-7.4.1"),

        // Play! Pokémon Penalty Guidelines.
        new RetrievalEvalCase(
            "How are penalties handled for a competitor with a history of repeat violations?",
            "PPG-4.2.2"),
        new RetrievalEvalCase(
            "A player pulled one more card off the top of their deck than they were supposed to. What penalty fits?",
            "PPG-5.5.1"),
        new RetrievalEvalCase(
            "A player picked up a prize card even though none of the opponent's Pokemon had been knocked out. How serious is that?",
            "PPG-5.5.1"),
        new RetrievalEvalCase(
            "If a judge gives a game loss halfway through a game, when does it actually take effect?",
            "PPG-4.1.1"),
        new RetrievalEvalCase(
            "A mistake can be undone so the game ends up exactly as it should have been. Can the judge go easier on the penalty?",
            "PPG-4.2.1"),
        new RetrievalEvalCase(
            "A player sat down at their table a few minutes after the round began. What penalty should they get?",
            "PPG-5.2.1"),
        new RetrievalEvalCase(
            "A deck check finds the player's list adds up to only 59 cards. What's the penalty?",
            "PPG-5.6.1"),
        new RetrievalEvalCase(
            "What does giving a player the two-prize penalty actually change about the game?",
            "PPG-3.3"),
        new RetrievalEvalCase(
            "A player deliberately lied to a judge to gain an edge. What should happen to them?",
            "PPG-5.4"),
        new RetrievalEvalCase(
            "A player keeps taking ages over every decision and their opponent is losing time. What can the judge do?",
            "PPG-5.7"),
        new RetrievalEvalCase(
            "How much extra time can a judge add to a round after spending a while on a ruling?",
            "PPG-4.4"),
        new RetrievalEvalCase(
            "A player muttered a curse word while chatting with a friend between games. Is there a penalty?",
            "PPG-5.3.1"),
        new RetrievalEvalCase(
            "If a player is thrown out of the event, do they still get the prizes they had earned?",
            "PPG-3.7"),

        // Pokémon TCG rulebook.
        new RetrievalEvalCase(
            "How many cards does a tournament deck need, and how many copies of one card can it run?",
            "TCGRULES-deck-building"),
        new RetrievalEvalCase(
            "Can a deck include two different ACE SPEC cards?",
            "TCGRULES-appendix-3-ace-spec-cards"),
        new RetrievalEvalCase(
            "How many Supporter cards can a player use during one turn?",
            "TCGRULES-turn-actions"),
        new RetrievalEvalCase(
            "A player has two different Pokemon-GX. Can they use a GX attack with each one in the same game?",
            "TCGRULES-appendix-19-pok-mon-gx"),
        new RetrievalEvalCase(
            "When a Pokemon attacks, in what order do you handle confusion, coin flips, choices and damage?",
            "TCGRULES-full-details-of-attacking"),
        new RetrievalEvalCase(
            "Only one player had to reshuffle and redraw their starting hand. How many extra cards can the other player take?",
            "TCGRULES-full-details-of-taking-a-mulligan"),
        new RetrievalEvalCase(
            "If a Pokemon is asleep and poisoned at once, what happens to it between turns?",
            "TCGRULES-special-conditions"),
        new RetrievalEvalCase(
            "Both players reached a winning condition at the same moment. Who wins?",
            "TCGRULES-what-if-both-players-win-at-the-same-time"),
        new RetrievalEvalCase(
            "A card says to draw five but there are only three cards left in the deck. Does the player lose?",
            "TCGRULES-what-if-you-should-draw-more-cards-than-you-have"),
        new RetrievalEvalCase(
            "Can a deck run two different Radiant Pokemon?",
            "TCGRULES-appendix-8-radiant-pok-mon"),
    };
}
