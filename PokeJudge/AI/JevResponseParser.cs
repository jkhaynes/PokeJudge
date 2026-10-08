namespace PokeJudge.AI;

using System.Text.Json;

// Pure parsing of a Jev /v1/systemone response for `noul` questions, kept apart from
// the HTTP call so it can be tested with canned JSON (same split as
// GeminiEmbeddingResponseParser). Shape confirmed against the live API (2026-10-04):
// { "answers": { "<key>": { "type": "noul", "noul": <0..1> } }, ... }. Fails loudly: an
// answer we can't read is a bug to fix, never a passage to silently rank last.
public static class JevResponseParser
{
    public static IReadOnlyList<double> Parse(string responseJson, IReadOnlyList<string> keys)
    {
        using var document = JsonDocument.Parse(responseJson);
        if (!document.RootElement.TryGetProperty("answers", out var answers) || answers.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"Jev response has no answers object: {responseJson}");
        }

        var scores = new List<double>(keys.Count);

        foreach (var key in keys)
        {
            if (!answers.TryGetProperty(key, out var answer))
            {
                throw new InvalidOperationException($"Jev response has no answer for question \"{key}\".");
            }

            if (answer.ValueKind != JsonValueKind.Object
                || !answer.TryGetProperty("noul", out var value)
                || value.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidOperationException($"Jev answer for \"{key}\" has no noul probability: {answer.GetRawText()}");
            }

            var probability = value.GetDouble();
            if (probability is < 0 or > 1)
            {
                throw new InvalidOperationException($"Jev answer for \"{key}\" is outside 0 to 1: {probability}");
            }

            scores.Add(probability);
        }

        return scores;
    }
}
