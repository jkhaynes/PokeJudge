namespace PokeJudge.Reliability;

using System.Text.Json;

// A distinct signal from RulingResult.SourceSupport (PRD SS9: "Confidence describes
// belief; Source Support describes evidence") -- the model's own, self-reported
// belief that its already-produced ruling is correct, captured by a separate call
// (ConfidenceEstimator) so it isn't just another field of the same generation pass.
// Never displayed to judges (PRD SS9) until Milestone 9's calibration analysis
// validates it, if it ever does -- purely an internal/evaluation-facing signal.
public sealed record ConfidenceEstimate(int PredictedCorrectnessProbability, string Rationale);

// Hand-written to mirror ConfidenceEstimate field-for-field, so the shape sent to
// Gemini and the shape deserialized back are visibly the same thing (same pattern
// as RulingResultSchema/ClarificationResultSchema).
public static class ConfidenceEstimateSchema
{
    public static readonly JsonElement Schema = JsonDocument.Parse("""
        {
          "type": "OBJECT",
          "properties": {
            "predictedCorrectnessProbability": { "type": "INTEGER" },
            "rationale": { "type": "STRING" }
          },
          "required": ["predictedCorrectnessProbability", "rationale"]
        }
        """).RootElement;
}
