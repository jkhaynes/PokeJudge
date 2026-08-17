namespace PokeJudge.Tests.TestDoubles;

using PokeJudge.Retrieval;

// Deterministic test double: pre-scripted per-call results, mirroring StubLlmClient.
// Lets ClarificationLoop's iterative-retrieval control flow (retrieve every turn,
// query text grows with confirmed facts) be tested without real embeddings.
public sealed class StubRetriever : IRetriever
{
    private readonly Queue<IReadOnlyList<ScoredChunk>> _results = new();

    public List<string> QueryTexts { get; } = new();
    public List<int> TopKValues { get; } = new();

    public void Enqueue(IReadOnlyList<ScoredChunk> results) => _results.Enqueue(results);

    public Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(string queryText, int topK)
    {
        QueryTexts.Add(queryText);
        TopKValues.Add(topK);

        if (_results.Count == 0)
        {
            throw new InvalidOperationException("StubRetriever has no more queued results.");
        }

        return Task.FromResult(_results.Dequeue());
    }
}
