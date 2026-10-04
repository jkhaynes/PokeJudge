namespace PokeJudge.Retrieval;

public sealed record RerankedCandidate(ScoredChunk Chunk, double Relevance, int CosineRank);

// Step 4 experiment: widen the cosine search to candidateCount, let an IRelevanceScorer
// judge which passages actually govern the situation, and pass on the best topK. A
// decorator, so ClarificationLoop and everything downstream is unchanged. Each
// ScoredChunk keeps its cosine Score -- only the order changes -- so the "score" shown
// in prompts (PromptBuilder) keeps its meaning. LINQ's OrderByDescending is stable, so
// equal relevance keeps cosine order.
public sealed class RerankingRetriever : IRetriever
{
    private readonly IRetriever _inner;
    private readonly IRelevanceScorer _scorer;
    private readonly int _candidateCount;
    private readonly Action<IReadOnlyList<RerankedCandidate>>? _onReranked;

    public RerankingRetriever(
        IRetriever inner,
        IRelevanceScorer scorer,
        int candidateCount,
        Action<IReadOnlyList<RerankedCandidate>>? onReranked = null)
    {
        if (candidateCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateCount), candidateCount, "Candidate count must be at least 1.");
        }

        _inner = inner;
        _scorer = scorer;
        _candidateCount = candidateCount;
        _onReranked = onReranked;
    }

    public async Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(string queryText, int topK)
    {
        if (topK > _candidateCount)
        {
            throw new ArgumentOutOfRangeException(nameof(topK), topK, $"topK ({topK}) cannot exceed the candidate count ({_candidateCount}).");
        }

        var candidates = await _inner.RetrieveAsync(queryText, _candidateCount);
        if (candidates.Count == 0)
        {
            return candidates;
        }

        var scores = await _scorer.ScoreAsync(queryText, candidates);
        if (scores.Count != candidates.Count)
        {
            throw new InvalidOperationException(
                $"Relevance scorer returned {scores.Count} score(s) for {candidates.Count} candidate(s).");
        }

        var reranked = candidates
            .Select((chunk, i) => new RerankedCandidate(chunk, scores[i], CosineRank: i + 1))
            .OrderByDescending(c => c.Relevance)
            .ToList();

        _onReranked?.Invoke(reranked);

        return reranked.Take(topK).Select(c => c.Chunk).ToList();
    }
}
