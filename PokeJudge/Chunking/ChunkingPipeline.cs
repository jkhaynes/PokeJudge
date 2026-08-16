namespace PokeJudge.Chunking;

using PokeJudge.AI;
using PokeJudge.Ingestion;

// Resumable by design: given the set of chunk IDs already embedded (loaded from a
// prior run's output file), only the missing chunks are sent to the embedding
// client, batched in groups of at most batchSize -- see
// .project-plans/milestone-4/plan.md's free-tier rate-limit note. Mirrors
// Milestone 2's ClarificationLoop pattern of injecting the untestable network
// boundary (IEmbeddingClient) so this orchestration is unit-testable.
public sealed class ChunkingPipeline
{
    private readonly IEmbeddingClient _embeddingClient;
    private readonly int _targetChunkSize;
    private readonly int _overlapSentences;
    private readonly int _batchSize;

    public ChunkingPipeline(
        IEmbeddingClient embeddingClient,
        int targetChunkSize = 800,
        int overlapSentences = 1,
        int batchSize = 100)
    {
        _embeddingClient = embeddingClient;
        _targetChunkSize = targetChunkSize;
        _overlapSentences = overlapSentences;
        _batchSize = batchSize;
    }

    public async Task<List<EmbeddedChunk>> RunAsync(
        IngestedDocument document,
        IReadOnlyDictionary<string, float[]> alreadyEmbedded,
        Action<List<EmbeddedChunk>>? onProgress = null)
    {
        var allChunks = new List<TextChunk>();
        foreach (var section in document.Sections)
        {
            var texts = TextChunker.Chunk(section.Text, _targetChunkSize, _overlapSentences);
            for (var i = 0; i < texts.Count; i++)
            {
                allChunks.Add(new TextChunk($"{section.SectionId}#{i}", section.SectionId, texts[i], section.Source));
            }
        }

        var toEmbed = allChunks.Where(c => !alreadyEmbedded.ContainsKey(c.ChunkId)).ToList();
        var newlyEmbedded = new Dictionary<string, float[]>();

        // Snapshot of every chunk embedded so far (pre-existing + completed this run),
        // in original document order. Chunks not yet embedded are simply omitted --
        // a partial snapshot is still a valid, resumable output.
        List<EmbeddedChunk> BuildSnapshot() => allChunks
            .Where(c => alreadyEmbedded.ContainsKey(c.ChunkId) || newlyEmbedded.ContainsKey(c.ChunkId))
            .Select(c => new EmbeddedChunk(
                c,
                alreadyEmbedded.TryGetValue(c.ChunkId, out var existing) ? existing : newlyEmbedded[c.ChunkId]))
            .ToList();

        for (var i = 0; i < toEmbed.Count; i += _batchSize)
        {
            var batch = toEmbed.Skip(i).Take(_batchSize).ToList();
            var vectors = await _embeddingClient.EmbedBatchAsync(batch.Select(c => c.Text).ToList());

            for (var j = 0; j < batch.Count; j++)
            {
                newlyEmbedded[batch[j].ChunkId] = vectors[j];
            }

            onProgress?.Invoke(BuildSnapshot());
        }

        return BuildSnapshot();
    }
}
