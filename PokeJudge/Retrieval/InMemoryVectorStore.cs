namespace PokeJudge.Retrieval;

using PokeJudge.Chunking;

public sealed record ScoredChunk(EmbeddedChunk Chunk, double Score);

// Brute-force linear-scan similarity search over chunks already loaded into
// memory. The right choice at this project's current scale (a few hundred
// chunks) -- no vector database, no approximate-nearest-neighbor indexing.
// See .project-plans/milestone-5/plan.md for why that's deferred, not missing.
public sealed class InMemoryVectorStore
{
    private readonly List<EmbeddedChunk> _chunks;

    public InMemoryVectorStore(IEnumerable<EmbeddedChunk> chunks)
    {
        _chunks = chunks.ToList();

        // Fail fast, at load time rather than mid-search, and name the specific
        // chunk(s) at fault -- a corrupted embedding must not be silently
        // dropped (a judge could end up with a ruling missing real material
        // it just never surfaced), but a raw "divide by zero"-shaped exception
        // deep in a search call gives no way to diagnose which chunk is bad.
        var invalidChunkIds = _chunks
            .Where(chunk => chunk.Embedding.Length == 0 || chunk.Embedding.All(v => v == 0f))
            .Select(chunk => chunk.Chunk.ChunkId)
            .ToList();

        if (invalidChunkIds.Count > 0)
        {
            throw new InvalidOperationException(
                "The following chunk(s) have an invalid (empty or all-zero) embedding vector, which " +
                "usually means the chunked/embedded output file is corrupted -- try re-running " +
                $"`chunk <document-code>` to regenerate it: {string.Join(", ", invalidChunkIds)}");
        }
    }

    public IReadOnlyList<ScoredChunk> Search(float[] queryVector, int topK)
    {
        return _chunks
            .Select(chunk => new ScoredChunk(chunk, VectorMath.CosineSimilarity(queryVector, chunk.Embedding)))
            .OrderByDescending(scored => scored.Score)
            .Take(topK)
            .ToList();
    }
}
