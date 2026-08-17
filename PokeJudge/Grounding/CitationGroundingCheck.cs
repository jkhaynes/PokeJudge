namespace PokeJudge.Grounding;

using System.Text.Json;

// The semantic half of PRD SS8's Source Support criteria -- unlike
// DeterministicGroundingChecks, classifying whether a cited passage actually
// supports its claim requires model judgment, not a lookup. See GroundingValidator
// and .project-plans/milestone-7/grounding-analysis.md for why this half cannot be
// made deterministic today.
public enum CitationSupportLevel
{
    ExplicitSupport,
    Interpretation,
    Unsupported
}

public sealed record CitationGroundingCheck(string ChunkId, CitationSupportLevel SupportLevel);

public sealed record GroundingAssessment(
    List<CitationGroundingCheck> Citations,
    bool ConflictDetected,
    string Rationale);

// Hand-written to mirror GroundingAssessment field-for-field, so the shape sent to
// Gemini and the shape deserialized back are visibly the same thing (same pattern
// as RulingResultSchema/ClarificationResultSchema).
public static class GroundingAssessmentSchema
{
    public static readonly JsonElement Schema = JsonDocument.Parse("""
        {
          "type": "OBJECT",
          "properties": {
            "citations": {
              "type": "ARRAY",
              "items": {
                "type": "OBJECT",
                "properties": {
                  "chunkId": { "type": "STRING" },
                  "supportLevel": {
                    "type": "STRING",
                    "enum": ["ExplicitSupport", "Interpretation", "Unsupported"]
                  }
                },
                "required": ["chunkId", "supportLevel"]
              }
            },
            "conflictDetected": { "type": "BOOLEAN" },
            "rationale": { "type": "STRING" }
          },
          "required": ["citations", "conflictDetected", "rationale"]
        }
        """).RootElement;
}
