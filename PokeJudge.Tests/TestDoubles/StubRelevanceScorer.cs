namespace PokeJudge.Tests.TestDoubles;

using PokeJudge.Retrieval;

// Deterministic test double: returns pre-scripted scores (or throws pre-scripted
// failures) and records each call, so RerankingRetriever's ordering logic and
// RetryingRelevanceScorer's retries are tested without a Jev call.
public sealed class StubRelevanceScorer : IRelevanceScorer
{
    private readonly Queue<Func<IReadOnlyList<double>>> _results = new();

    public List<string> Situations { get; } = new();
    public List<IReadOnlyList<ScoredChunk>> Candidates { get; } = new();

    public void Enqueue(params double[] scores) => _results.Enqueue(() => scores);

    public void EnqueueFailure(Exception failure) => _results.Enqueue(() => throw failure);

    public Task<IReadOnlyList<double>> ScoreAsync(string situation, IReadOnlyList<ScoredChunk> candidates)
    {
        Situations.Add(situation);
        Candidates.Add(candidates);

        if (_results.Count == 0)
        {
            throw new InvalidOperationException("StubRelevanceScorer has no more queued scores.");
        }

        return Task.FromResult(_results.Dequeue()());
    }
}
