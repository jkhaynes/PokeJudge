namespace PokeJudge.Grounding;

using PokeJudge.AI;
using PokeJudge.Clarification;
using PokeJudge.Retrieval;
using PokeJudge.StructuredState;

// PRD SS11's architecture diagram third box -- Grounding Validation & Source
// Support Assignment -- downstream of Ruling Generation, not something the ruling
// call also decides. Combines two genuinely different kinds of check: deterministic
// lookups (DeterministicGroundingChecks) and one LLM call classifying whether each
// cited passage actually supports its claim, a judgment the deterministic checks
// cannot make. This LLM call is not independent validation of RulingGenerator's own
// output -- both typically run against the same underlying model -- see
// .project-plans/milestone-7/grounding-analysis.md.
public sealed class GroundingValidator
{
    private readonly ILlmClient _llmClient;

    public GroundingValidator(ILlmClient llmClient)
    {
        _llmClient = llmClient;
    }

    public async Task<GroundingResult> ValidateAsync(
        RulingResult ruling, IReadOnlyList<ScoredChunk> retrievedChunks, bool clarificationWasSufficient)
    {
        var retrievalNonEmpty = DeterministicGroundingChecks.RetrievalNonEmpty(retrievedChunks);
        var allCitationsExist = DeterministicGroundingChecks.AllCitationsExist(ruling.CitedChunkIds, retrievedChunks);
        var factsWereSufficient = DeterministicGroundingChecks.FactsWereSufficient(clarificationWasSufficient);

        var assessment = await _llmClient.CompleteStructuredAsync<GroundingAssessment>(
            SystemPrompts.GroundingValidation,
            PromptBuilder.BuildGroundingValidationPrompt(ruling, retrievedChunks),
            GroundingAssessmentSchema.Schema);

        var (sourceSupport, rationale) = SourceSupportAssigner.Assign(
            ruling.CitedChunkIds, retrievalNonEmpty, allCitationsExist, factsWereSufficient, assessment);

        return new GroundingResult(
            sourceSupport, rationale, assessment, retrievalNonEmpty, allCitationsExist, factsWereSufficient);
    }
}

public sealed record GroundingResult(
    SourceSupport ValidatedSourceSupport,
    string ValidatedRationale,
    GroundingAssessment Assessment,
    bool RetrievalNonEmpty,
    bool AllCitationsExist,
    bool FactsWereSufficient);
