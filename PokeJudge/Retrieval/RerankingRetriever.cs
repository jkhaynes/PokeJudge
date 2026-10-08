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
    private readonly int? _maxPerSection;

    // maxPerSection caps how many excerpts of one section reach the top K, so a
    // section with many high-scoring excerpts can't crowd out every other rule
    // (supporter-twice: five PPG-4.2.1 excerpts pushed TCGRULES-turn-actions to 6th).
    // Skipped excerpts still fill any slots left when other sections run out.
    public RerankingRetriever(
        IRetriever inner,
        IRelevanceScorer scorer,
        int candidateCount,
        Action<IReadOnlyList<RerankedCandidate>>? onReranked = null,
        int? maxPerSection = null)
    {
        if (candidateCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(candidateCount), candidateCount, "Candidate count must be at least 1.");
        }

        if (maxPerSection < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxPerSection), maxPerSection, "Max per section must be at least 1.");
        }

        _inner = inner;
        _scorer = scorer;
        _candidateCount = candidateCount;
        _onReranked = onReranked;
        _maxPerSection = maxPerSection;
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

        return SelectTop(reranked, topK).Select(c => c.Chunk).ToList();
    }

    private IEnumerable<RerankedCandidate> SelectTop(IReadOnlyList<RerankedCandidate> reranked, int topK)
    {
        if (_maxPerSection is not { } max)
        {
            return reranked.Take(topK);
        }

        var kept = new List<RerankedCandidate>();
        var skipped = new List<RerankedCandidate>();
        var perSection = new Dictionary<string, int>();
        foreach (var candidate in reranked)
        {
            var section = candidate.Chunk.Chunk.Chunk.SectionId;
            var count = perSection.GetValueOrDefault(section);
            if (count < max)
            {
                perSection[section] = count + 1;
                kept.Add(candidate);
            }
            else
            {
                skipped.Add(candidate);
            }
        }

        return kept.Concat(skipped).Take(topK);
    }
}
