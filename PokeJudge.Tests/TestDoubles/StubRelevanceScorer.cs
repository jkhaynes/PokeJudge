namespace PokeJudge.Tests.TestDoubles;

using PokeJudge.Retrieval;

// Deterministic test double: returns pre-scripted scores and records each call, so
// RerankingRetriever's ordering logic is tested without a Jev call.
public sealed class StubRelevanceScorer : IRelevanceScorer
{
    private readonly Queue<IReadOnlyList<double>> _scores = new();

    public List<string> Situations { get; } = new();
    public List<IReadOnlyList<ScoredChunk>> Candidates { get; } = new();

    public void Enqueue(params double[] scores) => _scores.Enqueue(scores);

    public Task<IReadOnlyList<double>> ScoreAsync(string situation, IReadOnlyList<ScoredChunk> candidates)
    {
        Situations.Add(situation);
        Candidates.Add(candidates);

        if (_scores.Count == 0)
        {
            throw new InvalidOperationException("StubRelevanceScorer has no more queued scores.");
        }

        return Task.FromResult(_scores.Dequeue());
    }
}
