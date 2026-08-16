namespace PokeJudge.Tests.Chunking;

using PokeJudge.Chunking;
using PokeJudge.Ingestion;
using PokeJudge.Tests.TestDoubles;

public class ChunkingPipelineTests
{
    private static readonly SourceDocumentMetadata Source = new("Test Handbook", "May 21, 2026", null);

    private static IngestedDocument TwoSectionDocument() => new(
        Source,
        new List<IngestedSection>
        {
            new("TEST-1", "First", "This is the first section's text.", Source),
            new("TEST-2", "Second", "This is the second section's text.", Source),
        });

    [Fact]
    public async Task RunAsync_NoAlreadyEmbeddedChunks_EmbedsEveryChunk()
    {
        var stub = new StubEmbeddingClient();
        var pipeline = new ChunkingPipeline(stub, targetChunkSize: 200, overlapSentences: 0, batchSize: 100);

        var result = await pipeline.RunAsync(TwoSectionDocument(), new Dictionary<string, float[]>());

        Assert.Equal(2, result.Count);
        Assert.Equal("TEST-1#0", result[0].Chunk.ChunkId);
        Assert.Equal("TEST-2#0", result[1].Chunk.ChunkId);
        Assert.Single(stub.BatchCalls);
        Assert.Equal(2, stub.BatchCalls[0].Count);
    }

    [Fact]
    public async Task RunAsync_SomeChunksAlreadyEmbedded_OnlyRequestsTheMissingOnes()
    {
        var stub = new StubEmbeddingClient();
        var pipeline = new ChunkingPipeline(stub, targetChunkSize: 200, overlapSentences: 0, batchSize: 100);

        var alreadyEmbedded = new Dictionary<string, float[]>
        {
            ["TEST-1#0"] = new float[] { 99f },
        };

        var result = await pipeline.RunAsync(TwoSectionDocument(), alreadyEmbedded);

        Assert.Single(stub.BatchCalls);
        Assert.Single(stub.BatchCalls[0]);
        Assert.Equal("This is the second section's text.", stub.BatchCalls[0][0]);
    }

    [Fact]
    public async Task RunAsync_AlreadyEmbeddedChunk_KeepsItsStoredVectorUnchanged()
    {
        var stub = new StubEmbeddingClient();
        var pipeline = new ChunkingPipeline(stub, targetChunkSize: 200, overlapSentences: 0, batchSize: 100);

        var alreadyEmbedded = new Dictionary<string, float[]>
        {
            ["TEST-1#0"] = new float[] { 99f },
        };

        var result = await pipeline.RunAsync(TwoSectionDocument(), alreadyEmbedded);

        var firstChunkResult = result.Single(c => c.Chunk.ChunkId == "TEST-1#0");
        Assert.Equal(new float[] { 99f }, firstChunkResult.Embedding);
    }

    [Fact]
    public async Task RunAsync_ResultPreservesOriginalDocumentOrder()
    {
        var stub = new StubEmbeddingClient();
        var pipeline = new ChunkingPipeline(stub, targetChunkSize: 200, overlapSentences: 0, batchSize: 100);

        // TEST-1 already embedded (would otherwise be appended last if order weren't preserved).
        var alreadyEmbedded = new Dictionary<string, float[]> { ["TEST-1#0"] = new float[] { 99f } };

        var result = await pipeline.RunAsync(TwoSectionDocument(), alreadyEmbedded);

        Assert.Equal(new[] { "TEST-1#0", "TEST-2#0" }, result.Select(c => c.Chunk.ChunkId));
    }

    [Fact]
    public async Task RunAsync_MoreChunksThanBatchSize_SplitsIntoMultipleBatchCalls()
    {
        var stub = new StubEmbeddingClient();
        var pipeline = new ChunkingPipeline(stub, targetChunkSize: 200, overlapSentences: 0, batchSize: 1);

        var result = await pipeline.RunAsync(TwoSectionDocument(), new Dictionary<string, float[]>());

        Assert.Equal(2, result.Count);
        Assert.Equal(2, stub.BatchCalls.Count);
        Assert.All(stub.BatchCalls, call => Assert.Single(call));
    }

    [Fact]
    public async Task RunAsync_OnProgressCallback_FiresAfterEachBatchWithCumulativeSnapshotSoFar()
    {
        // A crash between batches (e.g. a rate limit) shouldn't lose already-embedded
        // chunks -- the caller uses this callback to persist progress incrementally.
        var stub = new StubEmbeddingClient();
        var pipeline = new ChunkingPipeline(stub, targetChunkSize: 200, overlapSentences: 0, batchSize: 1);

        var snapshots = new List<List<EmbeddedChunk>>();

        await pipeline.RunAsync(
            TwoSectionDocument(),
            new Dictionary<string, float[]>(),
            onProgress: snapshot => snapshots.Add(new List<EmbeddedChunk>(snapshot)));

        Assert.Equal(2, snapshots.Count);
        Assert.Single(snapshots[0]);
        Assert.Equal("TEST-1#0", snapshots[0][0].Chunk.ChunkId);
        Assert.Equal(2, snapshots[1].Count);
        Assert.Equal(new[] { "TEST-1#0", "TEST-2#0" }, snapshots[1].Select(c => c.Chunk.ChunkId));
    }

    [Fact]
    public async Task RunAsync_OnProgressCallback_NotProvided_StillWorksNormally()
    {
        var stub = new StubEmbeddingClient();
        var pipeline = new ChunkingPipeline(stub, targetChunkSize: 200, overlapSentences: 0, batchSize: 100);

        var result = await pipeline.RunAsync(TwoSectionDocument(), new Dictionary<string, float[]>());

        Assert.Equal(2, result.Count);
    }
}
