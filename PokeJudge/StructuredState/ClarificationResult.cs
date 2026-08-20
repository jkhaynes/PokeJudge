namespace PokeJudge.StructuredState;

using System.Text.Json;

public sealed record ClarifyingQuestion(string Question, string RelatedChunkId);

// No embedded draft ruling here -- Milestone 6 makes ruling generation its own
// explicit step (RulingGenerator), matching PRD SS11's architecture diagram,
// rather than something the sufficiency call also produces on the side.
//
// Rationale added in Milestone 8.5 as a diagnostic mirror of RulingResult's
// SourceSupportRationale -- before this, "insufficient with zero questions"
// (InsufficientWithoutQuestionsException) carried no information about why the
// model wouldn't commit to a question, making six repeated real reproductions of
// that crash undiagnosable. Defaults to "" so test doubles that don't care about
// rationale wiring don't need to supply one.
public sealed record ClarificationResult(bool IsSufficient, List<ClarifyingQuestion> Questions, string Rationale = "");

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
            },
            "rationale": { "type": "STRING" }
          },
          "required": ["isSufficient", "questions", "rationale"]
        }
        """).RootElement;
}
