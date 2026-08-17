# Milestone 7 — Implementation Summary

**Milestone:** Milestone 7 — Citations and Grounding
**Branch:** `milestone/7-citations-grounding`

## Milestone Implemented

Milestone 7 — Citations and Grounding (PRD roadmap #7). Builds PRD §11's "Grounding Validation & Source
Support Assignment" architecture box: Milestone 6's model-self-assigned `RulingResult.SourceSupport` label is
no longer trusted as-is. A new `Grounding/` module checks it against deterministic criteria (retrieval
non-empty, citation existence, fact sufficiency) plus a separate LLM call classifying whether each cited
passage actually supports its claim (`ExplicitSupport` / `Interpretation` / `Unsupported`), combined by a pure
`SourceSupportAssigner` function into a validated Strong/Partial/Insufficient label.

## What Changed

- **`Grounding/DeterministicGroundingChecks.cs`** (new) -- three pure static functions: `RetrievalNonEmpty`,
  `AllCitationsExist` (strengthened beyond a literal reading to also fail on zero citations, not just a
  fabricated one), `FactsWereSufficient`. No LLM, no I/O.
- **`Grounding/CitationGroundingCheck.cs`** (new) -- `CitationSupportLevel` enum, `CitationGroundingCheck` and
  `GroundingAssessment` records, and `GroundingAssessmentSchema` for the structured LLM output.
- **`Grounding/SourceSupportAssigner.cs`** (new) -- the pure combinator implementing PRD §8's rubric as code.
  A cited chunk ID the model's grounding-classification response drops is treated as `Unsupported`, not
  skipped, so a missing classification can't default to something favorable.
- **`Grounding/GroundingValidator.cs`** (new) -- orchestrates the three deterministic checks plus one LLM call
  (`SystemPrompts.GroundingValidation` + `PromptBuilder.BuildGroundingValidationPrompt`), returning a
  `GroundingResult` bundling the validated label/rationale with the raw assessment and each deterministic
  flag for console visibility.
- **`SystemPrompts.GroundingValidation`** (new) -- instructs the model to judge only whether a specific cited
  passage supports the specific claim it's attached to (a narrower, more mechanical task than re-judging the
  whole ruling), and to flag cross-passage conflict.
- **`PromptBuilder.BuildGroundingValidationPrompt`** (new) -- sends the ruling's recommendation/explanation
  plus each *cited* chunk's full text (not the whole retrieved set); a cited ID that isn't among the retrieved
  chunks is explicitly noted as "NOT FOUND" rather than silently omitted.
- **`Program.cs` wiring** -- after `RulingGenerator.GenerateAsync`, calls `GroundingValidator.ValidateAsync`
  and prints both labels side by side: "Model's own assessment (unvalidated)" and "Validated Source Support",
  plus the per-citation breakdown and the three deterministic-check flags.

No new NuGet packages. `Grounding/` is a new top-level module (not folded into `Clarification/`), matching
PRD §11's diagram, which draws grounding validation as a distinct third box downstream of ruling generation,
not something ruling generation also decides.

## Validation

- **Build:** `dotnet build` -- succeeded, 0 warnings, 0 errors.
- **Tests:** `dotnet test` -- 153/153 passed (126 carried over from the ingestion-pipeline work merged ahead
  of this branch, 27 new).
  - **Written first (Red step), all deterministic:** `DeterministicGroundingChecksTests` (7),
    `SourceSupportAssignerTests` (9, table-driven over the full rubric including the "missing citation
    treated as Unsupported" case), `GroundingValidatorTests` (4, stub-based, mirroring
    `RulingGeneratorTests`' orchestration/prompt-content pattern), `PromptBuilderTests` additions for
    `BuildGroundingValidationPrompt` (5).
  - **Final coverage-review pass:** surfaced one real gap the plan-driven tests didn't cover --
    `StructuredResponseParser`'s actual JSON deserialization of `GroundingAssessment` (including the
    `CitationSupportLevel` enum) was untested, since `GroundingValidatorTests` uses `StubLlmClient` and never
    exercises real JSON parsing. Added two tests there, matching the existing pattern for `RulingResult`'s
    `SourceSupport` enum deserialization test from Milestone 6.
- **Real-data validation** (see `observed-limitations.md` for full detail): ran the rebuilt flow against the
  real, expanded 4-document/515-chunk corpus for three scenarios --
  1. A well-covered scenario resolved in one turn with the validated label agreeing exactly with the model's
     own self-assessment (**Strong**/**Strong**), both citations `ExplicitSupport`.
  2. The Special Condition scenario from Milestone 6 produced a real, important **divergence**: the model's
     own self-assessment was **Partial** (correctly noting judge discretion was required), but grounding
     validation classified every citation `ExplicitSupport` and returned **Strong** -- on manual review, the
     model's original judgment looks more defensible. This is the opposite of what the plan anticipated
     (over-calling `Strong`, not under-correcting a `Partial`) and is documented as concrete evidence for the
     per-citation-granularity limitation the plan's Out-of-Scope section named but hadn't yet observed
     directly.
  3. Milestone 6's missed-Prize scenario, re-run against dramatically better retrieval (top score 0.80 on
     directly on-topic content vs. ~0.75 topically-adjacent before) still failed the identical way --
     `isSufficient: false` with zero clarifying questions, caught by the same Milestone 2 guard. Grounding
     validation never even ran for this scenario. Documented as evidence that better retrieval alone did not
     fix Milestone 6's failure mode -- it's a distinct weakness in the sufficiency-assessment step.

## Intentional Limitations

- **`GroundingValidator` is not independent validation.** It shares `Program.cs`'s single `ILlmClient`
  instance with `RulingGenerator` -- the same underlying model both generates and checks. `grounding-analysis.md`
  documents concretely why this matters, not just as a caveat.
- **Per-citation grounding granularity, not per-claim/per-sentence.** Real evidence now exists (scenario 2
  above) that this granularity can miss whether a ruling's overall synthesis across citations requires
  judgment, even when every individual citation checks out.
- **Source-conflict detection has never fired on a real positive case.** All three real runs returned
  `ConflictDetected: false`. Implemented but empirically unvalidated at this corpus scale.
- **`FactsWereSufficient` stays coarse** -- confirms the clarification loop reported `Sufficient` overall, not
  that every fact a *specific* cited passage depends on was confirmed rather than hypothesized.
- **No second, independent model/provider** for grounding validation -- noted as the concrete first step
  toward real independence in `grounding-analysis.md`, not built here.

## Learning Focus

- **Grounding as an explicit, separate step**, not a property asked for inside the same call that generates
  the ruling -- demonstrated with real evidence of the two labels actually disagreeing, not just architected
  to allow disagreement.
- **Citation existence (deterministic) vs. citation support (model judgment)** -- concretely different
  operations, only the first of which is currently automatable.
- **Source Support assembled by a pure, testable combinator function**, not asked for directly from a model --
  `SourceSupportAssigner` is the actual PRD §8 rubric, executable and unit-tested.
- **Same-model self-validation is not independent verification** -- not just asserted, but observed: the
  Special Condition scenario is a real case where the citation-level validation pipeline produced a *less*
  accurate label than the model's own more holistic self-report, which is itself informative about the limits
  of the validation design, not just the limits of self-report.

## What I Should Try

1. Re-read the Special Condition scenario's full output in `observed-limitations.md` §2 and decide for
   yourself: is `Strong` or `Partial` the more defensible label? This is a genuinely open, debatable case --
   good practice for the kind of judgment call Milestone 8's eval harness will eventually need to score.
2. Try to construct a scenario that actually makes `GroundingAssessment.ConflictDetected` fire -- it never has
   yet on this corpus. What would two genuinely conflicting retrieved passages look like here?
3. Re-run the missed-Prize scenario yourself and think about what's actually going wrong in the sufficiency
   step now that retrieval quality is no longer the bottleneck -- is it the prompt, the model, or something
   about how "insufficient with no questions" gets triggered?
4. Try deliberately feeding `RulingGenerator` a scenario where you expect a citation to be genuinely
   `Unsupported` (e.g., ask about something the retrieved passages only tangentially touch) and see whether
   `GroundingValidator` actually catches it, or whether it, too, tends to be generous.

## Git Status

- **Branch:** `milestone/7-citations-grounding`
- **Uncommitted:** yes -- all implementation changes are in the working tree, nothing staged or committed yet
  (this skill does not commit automatically).
- **Unexpected files:** none. `git status` shows exactly the expected changes: modified `Program.cs`,
  `Clarification/PromptBuilder.cs`, `Clarification/SystemPrompts.cs`, `PokeJudge.Tests/AI/StructuredResponseParserTests.cs`,
  `PokeJudge.Tests/Clarification/PromptBuilderTests.cs`; new `Grounding/` (production) and
  `PokeJudge.Tests/Grounding/` (tests) directories; new `.project-plans/milestone-7/` documents
  (`plan.md`, `grounding-analysis.md`, `observed-limitations.md`, this file).
