namespace PokeJudge.Retrieval;

// A judge-scenario-style natural language query paired with the section its
// answer should come from -- ground truth for the retrieval evaluation set.
public sealed record RetrievalEvalCase(string Query, string ExpectedSectionId);

public sealed record RetrievalEvalResult(RetrievalEvalCase Case, bool Hit, int? Rank, IReadOnlyList<ScoredChunk> TopResults);

// Deterministic hit/miss checking against already-computed search results --
// no embedding call, no LLM call. This is what "evaluable independent of the
// LLM" means concretely: this function's correctness has nothing to do with
// how good the underlying model or search is, only whether the expected
// section shows up in the results it's given.
public static class RetrievalEvaluator
{
    public static RetrievalEvalResult Evaluate(RetrievalEvalCase evalCase, IReadOnlyList<ScoredChunk> searchResults)
    {
        for (var i = 0; i < searchResults.Count; i++)
        {
            if (searchResults[i].Chunk.Chunk.SectionId == evalCase.ExpectedSectionId)
            {
                return new RetrievalEvalResult(evalCase, Hit: true, Rank: i + 1, searchResults);
            }
        }

        return new RetrievalEvalResult(evalCase, Hit: false, Rank: null, searchResults);
    }
}
