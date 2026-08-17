namespace PokeJudge.Tests.TestDoubles;

using PokeJudge.AI;

// Deterministic test double: returns a fixed-shape vector per input text (based on
// text length, so different inputs get visibly different vectors) without any real
// network call, and records exactly what was requested so tests can assert on
// batching/resumability behavior.
public sealed class StubEmbeddingClient : IEmbeddingClient
{
    public List<IReadOnlyList<string>> BatchCalls { get; } = new();

    public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts)
    {
        BatchCalls.Add(texts);

        var vectors = texts.Select(text => new float[] { text.Length }).ToList();
        return Task.FromResult<IReadOnlyList<float[]>>(vectors);
    }
}
