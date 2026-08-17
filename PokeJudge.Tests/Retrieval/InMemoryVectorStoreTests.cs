namespace PokeJudge.Tests.Retrieval;

using PokeJudge.Chunking;
using PokeJudge.Ingestion;
using PokeJudge.Retrieval;

public class InMemoryVectorStoreTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static EmbeddedChunk Chunk(string id, float[] embedding) =>
        new(new TextChunk(id, id, $"Text for {id}", Source), embedding);

    [Fact]
    public void Search_RanksResultsByDescendingSimilarity()
    {
        var store = new InMemoryVectorStore(new[]
        {
            Chunk("A", new float[] { 0f, 1f }),   // orthogonal to query -> 0.0
            Chunk("B", new float[] { 1f, 1f }),   // ~0.707 similarity to query
            Chunk("C", new float[] { 1f, 0f }),   // identical direction to query -> 1.0
        });

        var results = store.Search(new float[] { 1f, 0f }, topK: 3);

        Assert.Equal(new[] { "C", "B", "A" }, results.Select(r => r.Chunk.Chunk.ChunkId));
        Assert.True(results[0].Score > results[1].Score);
        Assert.True(results[1].Score > results[2].Score);
    }

    [Fact]
    public void Search_TopKLargerThanAvailableChunks_ReturnsAllChunks()
    {
        var store = new InMemoryVectorStore(new[]
        {
            Chunk("A", new float[] { 1f, 0f }),
            Chunk("B", new float[] { 0f, 1f }),
        });

        var results = store.Search(new float[] { 1f, 0f }, topK: 10);

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Search_TopKSmallerThanAvailableChunks_ReturnsOnlyTopK()
    {
        var store = new InMemoryVectorStore(new[]
        {
            Chunk("A", new float[] { 1f, 0f }),
            Chunk("B", new float[] { 0.9f, 0.1f }),
            Chunk("C", new float[] { 0f, 1f }),
        });

        var results = store.Search(new float[] { 1f, 0f }, topK: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal(new[] { "A", "B" }, results.Select(r => r.Chunk.Chunk.ChunkId));
    }

    [Fact]
    public void Search_EmptyStore_ReturnsEmptyResults()
    {
        var store = new InMemoryVectorStore(Array.Empty<EmbeddedChunk>());

        var results = store.Search(new float[] { 1f, 0f }, topK: 5);

        Assert.Empty(results);
    }

    [Fact]
    public void Search_TopKZero_ReturnsEmptyResults()
    {
        var store = new InMemoryVectorStore(new[] { Chunk("A", new float[] { 1f, 0f }) });

        var results = store.Search(new float[] { 1f, 0f }, topK: 0);

        Assert.Empty(results);
    }

    [Fact]
    public void Constructor_ChunkWithZeroVectorEmbedding_ThrowsNamingTheChunk()
    {
        // Fail fast at load time, not mid-search, and say which chunk is bad --
        // a corrupted/zero embedding shouldn't silently drop out of results, but
        // the failure needs to be diagnosable, not just a raw math exception.
        var chunks = new[]
        {
            Chunk("GOOD", new float[] { 1f, 0f }),
            Chunk("BAD-CHUNK", new float[] { 0f, 0f }),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => new InMemoryVectorStore(chunks));

        Assert.Contains("BAD-CHUNK", ex.Message);
    }

    [Fact]
    public void Constructor_MultipleChunksWithInvalidEmbeddings_NamesAllOfThem()
    {
        var chunks = new[]
        {
            Chunk("BAD-ONE", new float[] { 0f, 0f }),
            Chunk("GOOD", new float[] { 1f, 0f }),
            Chunk("BAD-TWO", Array.Empty<float>()),
        };

        var ex = Assert.Throws<InvalidOperationException>(() => new InMemoryVectorStore(chunks));

        Assert.Contains("BAD-ONE", ex.Message);
        Assert.Contains("BAD-TWO", ex.Message);
        Assert.DoesNotContain("GOOD", ex.Message);
    }

    [Fact]
    public void Constructor_AllValidEmbeddings_DoesNotThrow()
    {
        var chunks = new[]
        {
            Chunk("A", new float[] { 1f, 0f }),
            Chunk("B", new float[] { 0f, 1f }),
        };

        var exception = Record.Exception(() => new InMemoryVectorStore(chunks));

        Assert.Null(exception);
    }
}
