namespace PokeJudge.StructuredState;

using System.Text.Json;

public sealed record FactExtractionResult(List<string> ConfirmedFacts, List<string> Hypotheses);

public static class FactExtractionResultSchema
{
    public static readonly JsonElement Schema = JsonDocument.Parse("""
        {
          "type": "OBJECT",
          "properties": {
            "confirmedFacts": {
              "type": "ARRAY",
              "items": { "type": "STRING" }
            },
            "hypotheses": {
              "type": "ARRAY",
              "items": { "type": "STRING" }
            }
          },
          "required": ["confirmedFacts", "hypotheses"]
        }
        """).RootElement;
}
