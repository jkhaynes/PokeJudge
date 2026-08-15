namespace PokeJudge.Clarification;

public sealed record PolicySnippet(string Id, string Text);

public sealed record MockScenario(string Title, string Description, IReadOnlyList<PolicySnippet> Snippets);

// Milestone 2's stand-in for retrieval (no vector store/embeddings exist
// until Milestones 4-5). Each scenario carries 2-4 hand-authored, ID-tagged
// snippets -- some material to the ruling, some deliberately irrelevant or
// only partially relevant -- so materiality has to be reasoned from supplied
// text, not pretrained Pokemon knowledge. The app (not retrieval) decides
// which snippets belong to which scenario; that's the thing Milestone 6
// replaces with real retrieval behind the same ILlmClient interface.
public static class MockCorpus
{
    public static IReadOnlyList<MockScenario> Scenarios { get; } = new[]
    {
        new MockScenario(
            "Missed Prize Card",
            "During a League Challenge, a player realizes on their next turn that they forgot to take a " +
            "Prize card after knocking out their opponent's Pokemon two turns ago. What should the judge do?",
            new[]
            {
                new PolicySnippet(
                    "A1",
                    "Missed Prize Card - Discovery Timing: If a player forgets to take a Prize card after a " +
                    "Knock Out, and the error is discovered before that player's next turn ends, the Prize " +
                    "card is awarded immediately and play continues. If the error is discovered after the " +
                    "player has already ended their next turn, the Prize is NOT awarded retroactively; the " +
                    "judge corrects the Prize count only, and issues a Procedural Error penalty."),
                new PolicySnippet(
                    "A2",
                    "Procedural Error Penalty Tier: A first Procedural Error by a player in a match results " +
                    "in a Warning. A second Procedural Error by the same player in the same match results in " +
                    "a Game Loss."),
                new PolicySnippet(
                    "A3",
                    "Deck Check Procedure: At the start of a tournament, a judge may randomly select decks " +
                    "for a legality check against the submitted decklist."),
                new PolicySnippet(
                    "A4",
                    "Player Responsibility: Players are expected to call a judge as soon as they notice a " +
                    "game state error; delay in reporting does not itself change the remedy but should be " +
                    "noted by the judge."),
            }),
        new MockScenario(
            "Disputed Once-Per-Turn Ability",
            "A player used an Ability that says it can only be used once per turn, but the judge suspects " +
            "it may have been used twice in the same turn. The player disagrees. How should this be resolved?",
            new[]
            {
                new PolicySnippet(
                    "B1",
                    "Unverifiable Game State Disputes: When a factual dispute cannot be resolved from board " +
                    "state, game logs, or a corroborating witness, the judge must not issue a penalty based " +
                    "on suspicion alone. The judge should instead ensure the disputed action is not repeated " +
                    "this turn if a rules violation would otherwise occur."),
                new PolicySnippet(
                    "B2",
                    "Once-Per-Turn Abilities: An Ability restricted to once per turn resets at the start of " +
                    "that player's next turn, regardless of whether it was used earlier."),
                new PolicySnippet(
                    "B3",
                    "Special Condition: Confusion - Between turns, before drawing a card for the turn, a " +
                    "coin flip determines whether the Active Pokemon can attack this turn."),
                new PolicySnippet(
                    "B4",
                    "Investigation Standard: A judge should ask both players to describe the sequence of " +
                    "events and check for physical indicators (dice, markers, card orientation) before " +
                    "making a determination."),
            }),
        new MockScenario(
            "Illegal Deck Size Mid-Game",
            "A player's opponent points out, mid-game, that the player's deck has 61 cards instead of 60. " +
            "What should the judge do?",
            new[]
            {
                new PolicySnippet(
                    "C1",
                    "Illegal Deck Size Discovered Mid-Game: If a deck is found to contain more than 60 cards " +
                    "after the game has started, the player must randomize and remove the excess card(s) " +
                    "without looking at them, and receives a Procedural Error penalty. The game continues " +
                    "from the current point; it is not restarted."),
                new PolicySnippet(
                    "C2",
                    "Procedural Error Penalty Tier: A first Procedural Error by a player in a match results " +
                    "in a Warning. A second Procedural Error by the same player in the same match results in " +
                    "a Game Loss."),
                new PolicySnippet(
                    "C3",
                    "Prize Card Count: Standard matches are played to 6 Prize cards, awarded one per Knock " +
                    "Out unless a card effect specifies otherwise."),
                new PolicySnippet(
                    "C4",
                    "Decklist Mismatch: If the deck as played does not match the card counts on the player's " +
                    "registered decklist, in addition to any card-count penalty, the judge must correct the " +
                    "deck to match the registered decklist where possible."),
            }),
    };
}
