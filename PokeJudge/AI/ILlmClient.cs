namespace PokeJudge.AI;

using System.Text.Json;

// One method: every Milestone 2 call is schema-constrained. Milestone 1's
// free-text CompleteAsync is retired here -- nothing in the app calls it
// once the clarification loop is the main flow.
public interface ILlmClient
{
    Task<T> CompleteStructuredAsync<T>(string systemInstruction, string userContent, JsonElement responseSchema);
}
