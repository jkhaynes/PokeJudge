namespace PokeJudge.StructuredState;

using System.Text.Json;

public sealed record ClarifyingQuestion(string Question, string RelatedChunkId);

// No embedded draft ruling here -- Milestone 6 makes ruling generation its own
// explicit step (RulingGenerator), matching PRD SS11's architecture diagram,
// rather than something the sufficiency call also produces on the side.
public sealed record ClarificationResult(bool IsSufficient, List<ClarifyingQuestion> Questions);

// Hand-written to mirror ClarificationResult field-for-field, so the shape
// sent to Gemini and the shape deserialized back are visibly the same thing.
public static class ClarificationResultSchema
{
    public static readonly JsonElement Schema = JsonDocument.Parse("""
        {
          "type": "OBJECT",
          "properties": {
            "isSufficient": { "type": "BOOLEAN" },
            "questions": {
              "type": "ARRAY",
              "items": {
                "type": "OBJECT",
                "properties": {
                  "question": { "type": "STRING" },
                  "relatedChunkId": { "type": "STRING" }
                },
                "required": ["question", "relatedChunkId"]
              }
            }
          },
          "required": ["isSufficient", "questions"]
        }
        """).RootElement;
}
