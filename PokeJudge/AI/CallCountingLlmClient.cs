namespace PokeJudge.AI;

using System.Text.Json;

// Thin decorator counting calls made through it -- Milestone 9's `calibrate` command uses this
// to self-pace against the free-tier per-minute quota by tracking real usage instead of
// guessing costs from scenario type alone (see RunCalibration in Program.cs). A call that gets
// rejected (e.g. a 429) still counts: it was attempted and consumed a request slot against the
// quota, so undercounting it would pace too aggressively.
public sealed class CallCountingLlmClient : ILlmClient
{
    private readonly ILlmClient _inner;

    public int CallCount { get; private set; }

    public CallCountingLlmClient(ILlmClient inner)
    {
        _inner = inner;
    }

    public async Task<T> CompleteStructuredAsync<T>(string systemInstruction, string userContent, JsonElement responseSchema)
    {
        CallCount++;
        return await _inner.CompleteStructuredAsync<T>(systemInstruction, userContent, responseSchema);
    }
}
