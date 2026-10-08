namespace PokeJudge.Retrieval;

// One relevance score in [0, 1] per candidate, in input order: how strongly the passage
// governs the situation. An interface so RerankingRetriever's ordering logic is tested
// without the network (JevRelevanceScorer is the production implementation).
public interface IRelevanceScorer
{
    Task<IReadOnlyList<double>> ScoreAsync(string situation, IReadOnlyList<ScoredChunk> candidates);
}
