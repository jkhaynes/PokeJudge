namespace PokeJudge.Retrieval;

// Hand-authored, small (not statistically rigorous -- Milestone 8 formalizes
// that). Each query is phrased in plain judge language, deliberately not
// copying the source document's own wording, to exercise semantic search
// against literal keyword matching. Expected section IDs are grounded in the
// real Milestone 3/4 output (PPTRH, TCGTH), inspected directly before writing
// these, not guessed at.
public static class RetrievalEvalSet
{
    public static IReadOnlyList<RetrievalEvalCase> Cases { get; } = new[]
    {
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
            "How are penalties handled for a competitor with a history of repeat violations?",
            "PPTRH-7.5"),
        new RetrievalEvalCase(
            "What happens if a player has no Basic Pokemon in their opening hand?",
            "TCGTH-7.4.1"),
    };
}
