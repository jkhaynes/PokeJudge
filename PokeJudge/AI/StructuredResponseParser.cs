namespace PokeJudge.AI;

using System.Text.Json;

// Deliberately the counterpart to Milestone 1's NaiveSufficiencyParser: this
// deserializes schema-constrained model output straight into typed records
// instead of pattern-matching free text. It should behave consistently and
// fail loudly (JsonException) rather than guess when the model doesn't
// conform -- unlike the naive regex parser, whose whole point was to be
// unreliable.
public static class StructuredResponseParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static T Parse<T>(string rawJsonText)
    {
        return JsonSerializer.Deserialize<T>(rawJsonText, Options)
            ?? throw new InvalidOperationException($"Model response deserialized to a null {typeof(T).Name}.");
    }
}
