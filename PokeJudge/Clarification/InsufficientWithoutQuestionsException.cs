namespace PokeJudge.Clarification;

// Thrown when the model reports a scenario insufficient but supplies no clarifying
// questions -- Milestone 2's original guard against a malformed, unfollowable
// response. Kept as its own type, distinct from the generic InvalidOperationException
// AI/StructuredResponseParser throws for an unrelated failure (a structured response
// deserializing to null), so a caller -- notably Milestone 8's ScenarioEvalRunner --
// can catch this exact, known failure mode without also catching unrelated
// structured-output failures that happened to share a base exception type.
public sealed class InsufficientWithoutQuestionsException : Exception
{
    public InsufficientWithoutQuestionsException(string message) : base(message)
    {
    }
}
