namespace PokeJudge.AI;

using System.Text.Json;

// Pure parsing of a Jev /v1/systemone response for `noul` questions, kept apart from
// the HTTP call so it can be tested with canned JSON (same split as
// GeminiEmbeddingResponseParser). The field shape came from third-party guides, not
// the live API -- see the plan's Task 10 probe. Fails loudly: an answer we can't read
// is a bug to fix, never a passage to silently rank last.
public static class JevResponseParser
{
    public static IReadOnlyList<double> Parse(string responseJson, IReadOnlyList<string> keys)
    {
        using var document = JsonDocument.Parse(responseJson);
        var root = document.RootElement;
        var scores = new List<double>(keys.Count);

        foreach (var key in keys)
        {
            if (!root.TryGetProperty(key, out var answer))
            {
                throw new InvalidOperationException($"Jev response has no answer for question \"{key}\".");
            }

            double probability;
            if (answer.ValueKind == JsonValueKind.Number)
            {
                probability = answer.GetDouble();
            }
            else if (answer.ValueKind == JsonValueKind.Object
                && answer.TryGetProperty("probability", out var value)
                && value.ValueKind == JsonValueKind.Number)
            {
                probability = value.GetDouble();
            }
            else
            {
                throw new InvalidOperationException($"Jev answer for \"{key}\" has no probability: {answer.GetRawText()}");
            }

            if (probability is < 0 or > 1)
            {
                throw new InvalidOperationException($"Jev answer for \"{key}\" is outside 0 to 1: {probability}");
            }

            scores.Add(probability);
        }

        return scores;
    }
}
