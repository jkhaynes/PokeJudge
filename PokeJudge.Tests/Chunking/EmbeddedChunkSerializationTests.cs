namespace PokeJudge.Tests.Chunking;

using System.Text.Json;
using PokeJudge.Chunking;
using PokeJudge.Ingestion;

public class EmbeddedChunkSerializationTests
{
    [Fact]
    public void EmbeddedChunk_RoundTripsThroughJson()
    {
        var source = new SourceDocumentMetadata("Test Handbook", "May 21, 2026", null);
        var chunk = new TextChunk("TEST-1#0", "TEST-1", "Chunk text.", source);
        var embedded = new EmbeddedChunk(chunk, new float[] { 0.1f, 0.2f, 0.3f });

        var json = JsonSerializer.Serialize(embedded);
        var roundTripped = JsonSerializer.Deserialize<EmbeddedChunk>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(embedded.Chunk.ChunkId, roundTripped!.Chunk.ChunkId);
        Assert.Equal(embedded.Chunk.SectionId, roundTripped.Chunk.SectionId);
        Assert.Equal(embedded.Chunk.Text, roundTripped.Chunk.Text);
        Assert.Equal(embedded.Chunk.Source, roundTripped.Chunk.Source);
        Assert.Equal(embedded.Embedding, roundTripped.Embedding);
    }
}
