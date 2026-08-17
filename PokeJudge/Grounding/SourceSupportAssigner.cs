namespace PokeJudge.Grounding;

using PokeJudge.StructuredState;

// The actual "criteria-based, not raw model opinion" piece PRD SS8 requires: a
// pure function applying its Strong/Partial/Insufficient rubric as code, over
// inputs that are either fully deterministic (the three DeterministicGroundingChecks
// results) or a structured, itemized LLM output (GroundingAssessment) -- never the
// model's own free-form label. See .project-plans/milestone-7/grounding-analysis.md.
public static class SourceSupportAssigner
{
    public static (SourceSupport SourceSupport, string Rationale) Assign(
        IReadOnlyList<string> citedChunkIds,
        bool retrievalNonEmpty,
        bool allCitationsExist,
        bool factsWereSufficient,
        GroundingAssessment assessment)
    {
        if (!retrievalNonEmpty)
        {
            return (SourceSupport.Insufficient, "No passages were retrieved for this ruling.");
        }

        if (!allCitationsExist)
        {
            return (SourceSupport.Insufficient,
                "The ruling did not cite any retrieved passage, or cited a chunk ID that was not actually retrieved.");
        }

        if (!factsWereSufficient)
        {
            return (SourceSupport.Insufficient,
                "The clarification loop had not reported sufficiency when this ruling was generated.");
        }

        // The model was asked to classify each cited passage once. Two entries for the
        // same chunk ID is a malformed response, not something to silently resolve
        // (e.g. by taking the first) -- that could quietly pick a favorable
        // classification over a contradicting unfavorable one. Fail loudly instead,
        // naming the duplicate, per PRD SS9 and the same pattern used elsewhere in this
        // codebase for malformed structured output.
        var duplicateChunkIds = assessment.Citations
            .GroupBy(c => c.ChunkId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        if (duplicateChunkIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Grounding assessment classified the same chunk ID more than once: {string.Join(", ", duplicateChunkIds)}.");
        }

        // A cited ID the model didn't classify is treated as Unsupported, not
        // skipped -- a missing classification must not default to something
        // favorable.
        var supportByChunkId = assessment.Citations.ToDictionary(c => c.ChunkId, c => c.SupportLevel);
        var levels = citedChunkIds
            .Select(id => supportByChunkId.TryGetValue(id, out var level) ? level : CitationSupportLevel.Unsupported)
            .ToList();

        if (levels.Any(level => level == CitationSupportLevel.Unsupported))
        {
            return (SourceSupport.Insufficient,
                "At least one cited passage does not actually support the claim it was cited for.");
        }

        if (assessment.ConflictDetected || levels.Any(level => level == CitationSupportLevel.Interpretation))
        {
            return (SourceSupport.Partial,
                "Every citation exists and none is unsupported, but at least one requires interpretation or " +
                "judge discretion, or the retrieved passages conflict.");
        }

        return (SourceSupport.Strong,
            "Every citation exists, all facts were confirmed sufficient, and every cited passage explicitly " +
            "supports its claim with no conflicts.");
    }
}
