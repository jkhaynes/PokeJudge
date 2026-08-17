namespace PokeJudge.Retrieval;

using PokeJudge.AI;

// Production IRetriever: embeds the query text via Milestone 4's IEmbeddingClient,
// then delegates straight to Milestone 5's InMemoryVectorStore. No new embedding
// or similarity logic -- this is wiring, not a new capability.
public sealed class VectorStoreRetriever : IRetriever
{
    private readonly IEmbeddingClient _embeddingClient;
    private readonly InMemoryVectorStore _store;

    public VectorStoreRetriever(IEmbeddingClient embeddingClient, InMemoryVectorStore store)
    {
        _embeddingClient = embeddingClient;
        _store = store;
    }

    public async Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(string queryText, int topK)
    {
        var queryVectors = await _embeddingClient.EmbedBatchAsync(new[] { queryText });
        return _store.Search(queryVectors[0], topK);
    }
}
