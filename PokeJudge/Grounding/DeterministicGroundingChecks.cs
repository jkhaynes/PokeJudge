namespace PokeJudge.Grounding;

using PokeJudge.Retrieval;

// Pure, no-LLM checks over data already produced by retrieval/clarification/ruling
// generation -- the deterministic half of PRD SS8's Source Support criteria
// (retrieval success, citation coverage, fact sufficiency). No similarity
// thresholds or new retrieval logic here -- see .project-plans/milestone-7/plan.md's
// "Out of Scope".
public static class DeterministicGroundingChecks
{
    public static bool RetrievalNonEmpty(IReadOnlyList<ScoredChunk> retrievedChunks) =>
        retrievedChunks.Count > 0;

    // Strengthened beyond a literal "does every cited ID exist": a ruling that cites
    // nothing has no evidentiary support either, so an empty citation list also
    // fails this check rather than vacuously passing it.
    public static bool AllCitationsExist(IReadOnlyList<string> citedChunkIds, IReadOnlyList<ScoredChunk> retrievedChunks)
    {
        if (citedChunkIds.Count == 0)
        {
            return false;
        }

        var retrievedIds = retrievedChunks.Select(c => c.Chunk.Chunk.ChunkId).ToHashSet();
        return citedChunkIds.All(retrievedIds.Contains);
    }

    // Trivial by design -- this turns an already-enforced control-flow guarantee
    // (Program.cs never calls RulingGenerator on a TurnCapExhausted outcome) into an
    // explicit, testable criterion the Source Support assignment itself checks,
    // rather than an implicit assumption about caller discipline (PRD SS8).
    public static bool FactsWereSufficient(bool clarificationWasSufficient) => clarificationWasSufficient;
}
