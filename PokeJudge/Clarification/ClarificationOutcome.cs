namespace PokeJudge.Clarification;

using PokeJudge.StructuredState;

// No embedded ruling here -- once Sufficient, the caller runs one more
// retrieval against the complete accumulated scenario and calls RulingGenerator
// (PRD FR7, SS11), rather than the loop producing a rough draft itself.
public sealed record ClarificationOutcome(bool Sufficient, GameState State, int TurnsUsed)
{
    public static ClarificationOutcome SufficientAt(GameState state, int turn) =>
        new(true, state, turn);

    public static ClarificationOutcome TurnCapExhausted(GameState state, int maxTurns) =>
        new(false, state, maxTurns);
}
