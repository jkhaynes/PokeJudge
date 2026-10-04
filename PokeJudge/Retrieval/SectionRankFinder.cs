namespace PokeJudge.Retrieval;

// Where a reranker could help: InTopK is already sent to the AI; Rerankable sits in the
// wider candidate list (ranks topK+1..N), where reranking can promote it; OutOfReach
// isn't in the candidates at all, so no reranker over them can recover it.
public enum SectionReach
{
    InTopK,
    Rerankable,
    OutOfReach
}

public sealed record SectionRank(string SectionId, int? Rank, SectionReach Reach);

// Pure. Reuses RetrievalEvaluator's first-match-by-SectionId rank, so "rank" means the
// same thing here as in the search-only test.
public static class SectionRankFinder
{
    public static IReadOnlyList<SectionRank> Find(IReadOnlyList<string> sectionIds, IReadOnlyList<ScoredChunk> results, int topK) =>
        sectionIds
            .Select(sectionId =>
            {
                var rank = RetrievalEvaluator.Evaluate(new RetrievalEvalCase(string.Empty, sectionId), results).Rank;
                var reach = rank is null ? SectionReach.OutOfReach
                    : rank <= topK ? SectionReach.InTopK
                    : SectionReach.Rerankable;
                return new SectionRank(sectionId, rank, reach);
            })
            .ToList();
}
