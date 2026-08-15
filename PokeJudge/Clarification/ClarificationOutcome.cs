namespace PokeJudge.Clarification;

using PokeJudge.StructuredState;

public sealed record ClarificationOutcome(bool Sufficient, GameState State, DraftRuling? Draft, int TurnsUsed)
{
    public static ClarificationOutcome SufficientAt(GameState state, DraftRuling draft, int turn) =>
        new(true, state, draft, turn);

    public static ClarificationOutcome TurnCapExhausted(GameState state, int maxTurns) =>
        new(false, state, null, maxTurns);
}
