namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Chunking;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;
using PokeJudge.Tests.TestDoubles;

public class VectorStoreRetrieverTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static EmbeddedChunk Chunk(string id, float[] embedding) =>
        new(new TextChunk(id, id, $"Text for {id}", Source), embedding);

    // StubEmbeddingClient derives a 1-dimensional vector from the query text's
    // length, so a chunk embedding with the matching length is the closest match.
    [Fact]
    public async Task RetrieveAsync_EmbedsTheQueryAndDelegatesToTheVectorStore()
    {
        var store = new InMemoryVectorStore(new[]
        {
            Chunk("A", new float[] { 3f }),
            Chunk("B", new float[] { 99f }),
        });
        var embeddingClient = new StubEmbeddingClient();
        var retriever = new VectorStoreRetriever(embeddingClient, store);

        var results = await retriever.RetrieveAsync("abc", topK: 1);

        Assert.Single(embeddingClient.BatchCalls);
        Assert.Equal(new[] { "abc" }, embeddingClient.BatchCalls[0]);
        Assert.Single(results);
        Assert.Equal("A", results[0].Chunk.Chunk.ChunkId);
    }

    [Fact]
    public async Task RetrieveAsync_RespectsTopK()
    {
        var store = new InMemoryVectorStore(new[]
        {
            Chunk("A", new float[] { 3f }),
            Chunk("B", new float[] { 3f }),
            Chunk("C", new float[] { 3f }),
        });
        var retriever = new VectorStoreRetriever(new StubEmbeddingClient(), store);

        var results = await retriever.RetrieveAsync("abc", topK: 2);

        Assert.Equal(2, results.Count);
    }
}
