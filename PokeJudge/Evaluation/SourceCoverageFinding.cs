namespace PokeJudge.Evaluation;

// The four source-coverage classifications PRD SS15 defines. This records a human
// judgment call -- inspecting a scenario's actually-retrieved chunks against the
// real corpus and asking whether a knowledgeable human judge would find them
// sufficient -- not an automated scoring criterion. Deliberately not an
// LLM-as-judge check, consistent with this project's standing position on
// self/model validation (Milestone 7's grounding-analysis.md, Milestone 8's
// rejection of LLM-as-judge ruling-quality scoring).
public enum SourceCoverageLevel
{
    // The retrieved official material contains enough information for a
    // knowledgeable human judge to resolve the scenario. A PokeJudge failure here
    // points elsewhere in the pipeline (sufficiency assessment, clarification,
    // ruling generation, or grounding), not at retrieval or the corpus.
    Sufficient,

    // The corpus contains what's needed, but retrieval didn't surface the right or
    // complete material -- points to ranking, chunking, or query formulation, not
    // missing sources.
    RetrievalProblem,

    // Retrieved material is relevant but appears insufficient to fully resolve the
    // scenario. The missing information is recorded, but the gap isn't yet
    // confirmed against the real source documents.
    PossibleSourceGap,

    // A specific authoritative source containing the needed information is known
    // but not currently in the corpus.
    ConfirmedSourceGap,
}

// A recorded finding for one repeatedly-failing or unexpectedly-behaving scenario --
// written up manually per scenario in
// .project-plans/milestone-8.5/source-coverage-analysis.md, not recomputed by the
// harness on every run.
public sealed record SourceCoverageFinding(
    string ScenarioId,
    SourceCoverageLevel Level,
    string Notes,
    string? MissingInformation = null,
    string? LikelySourceDocument = null);
