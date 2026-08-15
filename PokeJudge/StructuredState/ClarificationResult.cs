namespace PokeJudge.StructuredState;

using System.Text.Json;

public sealed record ClarifyingQuestion(string Question, string RelatedSnippetId);

public sealed record DraftRuling(string RecommendedAction, List<string> SupportingSnippetIds);

public sealed record ClarificationResult(bool IsSufficient, List<ClarifyingQuestion> Questions, DraftRuling? Draft);

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
                  "relatedSnippetId": { "type": "STRING" }
                },
                "required": ["question", "relatedSnippetId"]
              }
            },
            "draft": {
              "type": "OBJECT",
              "nullable": true,
              "properties": {
                "recommendedAction": { "type": "STRING" },
                "supportingSnippetIds": {
                  "type": "ARRAY",
                  "items": { "type": "STRING" }
                }
              },
              "required": ["recommendedAction", "supportingSnippetIds"]
            }
          },
          "required": ["isSufficient", "questions"]
        }
        """).RootElement;
}
