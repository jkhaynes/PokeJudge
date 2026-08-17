namespace PokeJudge.AI;

using System.Text.Json;

// Pure, deterministic parsing logic split out of GeminiEmbeddingClient's HTTP call --
// same reasoning as Milestone 2's StructuredResponseParser: the shape/count
// validation is testable without touching the network, and fails with a clear,
// named exception (PRD S9 "fail visibly") rather than an opaque
// IndexOutOfRangeException from positional indexing further downstream.
public static class GeminiEmbeddingResponseParser
{
    public static IReadOnlyList<float[]> Parse(string responseJson, int expectedCount)
    {
        using var doc = JsonDocument.Parse(responseJson);

        if (!doc.RootElement.TryGetProperty("embeddings", out var embeddings))
        {
            throw new InvalidOperationException(
                $"Gemini embedding API response had no \"embeddings\" field: {doc.RootElement}");
        }

        var actualCount = embeddings.GetArrayLength();
        if (actualCount != expectedCount)
        {
            throw new InvalidOperationException(
                $"Gemini embedding API returned {actualCount} embedding(s) but {expectedCount} text(s) were requested.");
        }

        var vectors = new List<float[]>(expectedCount);
        foreach (var embedding in embeddings.EnumerateArray())
        {
            var values = embedding.GetProperty("values");
            var vector = new float[values.GetArrayLength()];
            var i = 0;
            foreach (var value in values.EnumerateArray())
            {
                vector[i++] = value.GetSingle();
            }
            vectors.Add(vector);
        }

        return vectors;
    }
}
