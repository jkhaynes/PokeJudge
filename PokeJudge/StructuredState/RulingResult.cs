namespace PokeJudge.StructuredState;

using System.Text.Json;

// PRD SS8's judge-facing reliability signal: Source Support describes how well
// retrieved material backs the recommendation. It is NOT the model's confidence.
// In this milestone it is purely the model's own labeling against the rubric in
// SystemPrompts.RulingGeneration -- nothing here checks it against retrieval
// success, citation coverage, or fact sufficiency yet. That validation is
// Milestone 7's job; this is intentionally a first pass, not a finished signal.
public enum SourceSupport
{
    Strong,
    Partial,
    Insufficient
}

public sealed record RulingResult(
    string Recommendation,
    string Explanation,
    List<string> RepairSteps,
    string? PenaltyGuidance,
    List<string> CitedChunkIds,
    SourceSupport SourceSupport,
    string SourceSupportRationale);

public static class RulingResultSchema
{
    public static readonly JsonElement Schema = JsonDocument.Parse("""
        {
          "type": "OBJECT",
          "properties": {
            "recommendation": { "type": "STRING" },
            "explanation": { "type": "STRING" },
            "repairSteps": {
              "type": "ARRAY",
              "items": { "type": "STRING" }
            },
            "penaltyGuidance": { "type": "STRING", "nullable": true },
            "citedChunkIds": {
              "type": "ARRAY",
              "items": { "type": "STRING" }
            },
            "sourceSupport": {
              "type": "STRING",
              "enum": ["Strong", "Partial", "Insufficient"]
            },
            "sourceSupportRationale": { "type": "STRING" }
          },
          "required": [
            "recommendation",
            "explanation",
            "repairSteps",
            "citedChunkIds",
            "sourceSupport",
            "sourceSupportRationale"
          ]
        }
        """).RootElement;
}
