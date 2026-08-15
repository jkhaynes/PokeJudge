namespace PokeJudge.Tests.TestDoubles;

using System.Text.Json;
using PokeJudge.AI;

// Deterministic test double: results are pre-scripted, so tests exercising
// ClarificationLoop's control flow (turn cap, stop-on-sufficient, fact
// accumulation) can be fully deterministic even though the real LLM isn't.
public sealed class StubLlmClient : ILlmClient
{
    private readonly Queue<object> _results = new();

    public List<string> UserContents { get; } = new();

    public void Enqueue(object structuredResult) => _results.Enqueue(structuredResult);

    public Task<T> CompleteStructuredAsync<T>(string systemInstruction, string userContent, JsonElement responseSchema)
    {
        UserContents.Add(userContent);

        if (_results.Count == 0)
        {
            throw new InvalidOperationException("StubLlmClient has no more queued results.");
        }

        return Task.FromResult((T)_results.Dequeue());
    }
}
