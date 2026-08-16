namespace PokeJudge.AI;

// A second, differently-shaped provider abstraction alongside ILlmClient:
// text-in/vector-out, not prompt-in/structured-JSON-out. Batch-first (not one
// call per text) specifically to keep embedding-API request counts down --
// see .project-plans/milestone-4/plan.md's free-tier rate-limit note.
public interface IEmbeddingClient
{
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts);
}
