namespace PokeJudge.Retrieval;

// Small seam over "embed a query, then search the vector store" -- exists for
// the same reason ILlmClient does: ClarificationLoop calls this every turn now
// (Milestone 6's iterative retrieval), and a real embedding call per turn would
// make the loop's control flow impossible to unit test deterministically.
public interface IRetriever
{
    Task<IReadOnlyList<ScoredChunk>> RetrieveAsync(string queryText, int topK);
}
