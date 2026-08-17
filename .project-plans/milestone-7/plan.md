# Milestone 7 — Citations and Grounding

Status: planned, not started
Source: PRD.md §7-8 (Functional/AI-Specific Requirements, esp. the Source Support criteria), §9
(Reliability/Safety), §11 (architecture — the "Grounding Validation & Source Support Assignment" box),
"Learning Objectives → Milestone 7"

## 1. What We Will Build

Milestone 6 wired real retrieval into generation and produced a `RulingResult` that includes a
`SourceSupport` label — but that label is purely the model's own free-form judgment, asserted inside the
same call that generates the ruling, checked against nothing. The Milestone 6 review flagged this directly:
`RulingResult.CitedChunkIds` is never verified against what was actually retrieved, and nothing checks
whether a cited passage actually supports the claim it's attached to. PRD §11's architecture diagram draws
this as its own box — **Grounding Validation & Source Support Assignment** — downstream of Ruling Generation,
not something Ruling Generation quietly also decides. Milestone 7 builds that box.

Concretely:

1. **A new `Grounding/` module**, matching the diagram's explicit third box (distinct from both the
   Sufficiency/Clarification engine and Ruling Generation). Unlike `RulingGenerator` — which Milestone 6
   correctly kept inside `Clarification/` as the loop's natural terminal step — grounding validation operates
   on a ruling *after* it exists, checking it against retrieval/citation/fact data the ruling generation call
   already had access to but wasn't independently checked against. That's a distinct responsibility, not a
   continuation of clarification.
2. **Deterministic grounding checks** (`Grounding/DeterministicGroundingChecks.cs`), each a pure function
   over data already in hand — no LLM call:
   - *Retrieval success*: was the retrieved chunk set non-empty?
   - *Citation existence*: does every ID in `RulingResult.CitedChunkIds` actually appear in the chunk set
     that was supplied to `RulingGenerator` for that call? (The exact gap the Milestone 6 review named.)
   - *Fact sufficiency*: did the clarification loop actually report sufficiency before this ruling was
     generated, i.e. is `RulingGenerator` being called on a real "sufficient" outcome and not a bypass? This
     is currently only enforced by `Program.cs`'s control flow (it never calls `RulingGenerator` on
     `TurnCapExhausted`) — Milestone 7 makes it an explicit, testable criterion the Source Support assignment
     itself checks, rather than an implicit guarantee of caller discipline.
3. **A semantic grounding check that genuinely requires model judgment**
   (`Grounding/GroundingValidator.cs` + `SystemPrompts.GroundingValidation` +
   `PromptBuilder.BuildGroundingValidationPrompt`): one more LLM call, separate from ruling generation, that
   takes the ruling's recommendation/explanation, each cited chunk's ID and text, and classifies each
   citation as one of three states — directly matching PRD §7's "distinguish explicit policy / reasonable
   interpretation / insufficient information" framing at the level Milestone 6 already established
   (per-citation, not per-sentence):
   - **ExplicitSupport** — the cited passage directly and explicitly backs the claim it's attached to.
   - **Interpretation** — the cited passage is relevant but the claim requires judge discretion or reasonable
     inference beyond what the passage explicitly states.
   - **Unsupported** — the cited passage does not actually support the claim (a citation that exists but
     doesn't hold up under scrutiny — the case Milestone 6 could not detect at all).
   Also asks the model to flag any direct conflict between two or more retrieved passages relevant to the
   same claim.
4. **A deterministic Source Support combinator** (`Grounding/SourceSupportAssigner.cs`): a pure function —
   the actual "criteria-based, not raw model opinion" piece PRD §8 requires — taking the deterministic-check
   results plus the (LLM-produced, but now structured and itemized) per-citation grounding verdicts, and
   computing the final Source Support label + rationale by applying PRD §8's rubric as code: retrieval
   empty, a citation that doesn't exist, insufficient facts, or any `Unsupported` citation forces
   `Insufficient`; any `Interpretation` citation or a flagged conflict caps the result at `Partial`; only
   all-`ExplicitSupport`-with-no-conflict reaches `Strong`. This function is what actually gets unit tested
   thoroughly (pure, deterministic, no stubs needed) — the semantic classification feeding into it is a
   separate, LLM-backed concern with its own test double.
5. **Console wiring**: after `RulingGenerator` produces its result, run it through `GroundingValidator`, then
   print *both* labels side by side — the model's original self-assigned Source Support (from Milestone 6,
   explicitly labeled as the model's own unchecked opinion) and the new validated Source Support (labeled as
   the actual judge-facing signal) — so any divergence between them is directly visible, not buried.
6. **A required written analysis** (`.project-plans/milestone-7/grounding-analysis.md`): which grounding
   checks are deterministic application logic (retrieval-non-empty, citation-ID existence, fact-sufficiency
   status, and the final Strong/Partial/Insufficient combinator itself) versus which genuinely require model
   judgment (per-citation semantic support, conflict detection) — and an explicit discussion of why
   `GroundingValidator` calling the same underlying model that `RulingGenerator` used is *not* truly
   independent validation (correlated blind spots can survive the check), per the Milestone 7 learning
   objective in the PRD.

### Reusing, not duplicating, Milestone 6's building blocks

`GroundingValidator` takes exactly the same shape of data `RulingGenerator` already produces and consumes —
`RulingResult`, the `IReadOnlyList<ScoredChunk>` used to generate it, and the `GameState`/sufficiency outcome
— no new retrieval, embedding, or game-state logic. The existing `SourceSupport` enum
(`StructuredState/RulingResult.cs`) is reused as-is for the validated label; no duplicate enum.

## 2. AI Concepts Being Learned

- **Grounding and hallucination mitigation as an explicit, separate step** — not a property you ask the
  generation call to also self-report, but something checked afterward against independently available
  data.
- **Citation-ID existence vs. citation *support*** — the difference between "did the model cite something
  real" (a lookup, fully deterministic) and "does what it cited actually say what the model claims it says"
  (a reading-comprehension judgment that, today, still requires a model).
- **Why Source Support must be assembled by a deterministic combinator, not asked for directly.** PRD §8 is
  explicit that Source Support has to be "derived from testable, observable conditions" — Milestone 7 is
  where that stops being an assertion and becomes a real function with real inputs and outputs.
- **The limits of self-validation.** `GroundingValidator` uses the same LLM provider/model as
  `RulingGenerator`. Documenting concretely why that is not independent verification — and what would be
  needed for real independence (a different model, a human-in-the-loop, or ground-truth eval data) — is a
  required deliverable, not an aside.
- **"I don't know" as a designed, expected outcome.** An `Unsupported` citation or an empty retrieval set
  should visibly and correctly collapse the final label to `Insufficient`, not get smoothed over.

## 3. Implementation Steps (in order)

1. **Add `Grounding/DeterministicGroundingChecks.cs`**: pure static functions — `RetrievalNonEmpty`,
   `AllCitationsExist`, `FactsWereSufficient` — each taking only data already produced elsewhere
   (`RulingResult`, the retrieved chunk list, and the clarification outcome's sufficiency flag). No LLM, no
   I/O.
2. **Add `Grounding/CitationGroundingCheck.cs`**: a small record type — `ChunkId`, a three-way
   `CitationSupportLevel` enum (`ExplicitSupport` / `Interpretation` / `Unsupported`) — plus a
   `GroundingAssessment` record bundling the per-citation list with a `ConflictDetected` flag and rationale.
   Add the JSON schema for the structured LLM output that produces this shape.
3. **Add `SystemPrompts.GroundingValidation`** + **`PromptBuilder.BuildGroundingValidationPrompt`**: given the
   ruling's recommendation/explanation and each cited chunk's ID + text, ask the model to classify each
   citation's support level and flag conflicts. Explicit instruction: judge only whether the *cited* text
   supports the *specific* claim it's attached to — this is a narrower, more mechanical task than "was this a
   good ruling," which keeps the check meaningfully different from re-doing ruling generation.
4. **Add `Grounding/GroundingValidator.cs`**: takes an `ILlmClient`, `RulingResult`, the retrieved chunks,
   and the clarification outcome; runs the deterministic checks, makes the one grounding-classification LLM
   call, and returns a combined result.
5. **Add `Grounding/SourceSupportAssigner.cs`**: the pure combinator function implementing PRD §8's rubric —
   inputs are the deterministic check results plus the `GroundingAssessment`; output is `(SourceSupport,
   string rationale)`. This is the piece with the most unit tests, since it's fully deterministic and the
   actual PRD-mandated logic.
6. **Wire `Program.cs`**: after `RulingGenerator.GenerateAsync`, call `GroundingValidator`, then print the
   model's original `RulingResult.SourceSupport`/`SourceSupportRationale` labeled as "model's own
   assessment (unvalidated)" alongside the new validated label and rationale labeled as "validated Source
   Support" — plus the per-citation breakdown so a judge (and the developer) can see exactly which citations
   held up.
7. **Run the real end-to-end flow** against the real corpus for at least three scenarios: one where the
   validated label agrees with the model's self-assessment, one deliberately chosen to try to make the model
   over-call `Strong` on a citation that's actually only interpretive (the case Milestone 6's review flagged
   as worth checking), and a re-run of Milestone 6's weakly-covered missed-Prize scenario if it now reaches
   ruling generation at all (it may still crash earlier at the sufficiency step — that's fine to observe
   again, not a regression to fix here).
8. **Update/add tests**: `DeterministicGroundingChecksTests` (pure, thorough), `SourceSupportAssignerTests`
   (pure, thorough — table-driven over the rubric's cases), `PromptBuilderTests` additions for the new
   prompt, `GroundingValidatorTests` (stub-based, mirroring `RulingGeneratorTests`' pattern of asserting both
   orchestration and actual prompt content).
9. **Write `grounding-analysis.md`**: the required deterministic-vs-model-judgment breakdown and the
   same-model-self-validation limitation discussion.
10. **Document observed findings** in `observed-limitations.md`: at least one real instance of the validated
    label disagreeing with the model's self-assessment, and a candid note on whether the conflict-detection
    check ever actually fired against this corpus.

## 4. Expected Limitations / Failures to Intentionally Observe

- **`GroundingValidator` is not independent validation.** It very likely calls the same underlying Gemini
  model `RulingGenerator` used. A model that hallucinated a claim in generation may well also (incorrectly)
  vouch for that claim when asked to check it — this is the central, required learning point of this
  milestone, not a bug to fix.
- **The semantic support classification (ExplicitSupport / Interpretation / Unsupported) is itself an LLM
  judgment**, with all the reliability caveats that implies — expect at least one case where the
  classification looks debatable on manual inspection, and document it rather than silently picking a
  favorable example.
- **Source-conflict detection may rarely or never fire** against this project's current two-document corpus
  at this scale — worth explicitly noting if the real runs never exercise it, rather than assuming it works
  because it's implemented.
- **Fact-sufficiency here stays coarse**: `FactsWereSufficient` only confirms the clarification loop reported
  `Sufficient` overall, not that every individual fact a *specific cited passage* depends on was confirmed
  rather than hypothesized. A deeper per-citation fact-dependency check is a real limitation, not solved
  here.
- **One more LLM call per ruling** (the grounding-validation call) — further real, observable latency/cost
  growth on top of Milestone 6's, consistent with the project's running theme of not optimizing this away
  until it's a demonstrated problem.

## 5. What I Should Understand by the End

- The concrete difference between a citation *existing* (deterministic lookup) and a citation *supporting*
  its claim (a judgment call), and why only the first is safe to fully automate today.
- Why Source Support is now assembled by a pure, testable combinator function instead of being asked for
  directly from the model — and what inputs that function actually needs to be trustworthy.
- Why using the same model to generate and to validate is not truly independent verification, concretely —
  not just as a stated caveat, but demonstrated by at least one real case where it plausibly failed to catch
  its own error.
- What "distinguishing explicit policy from reasonable interpretation from insufficient information" looks
  like as actual structured output, at the citation level, rather than an abstract product requirement.

## Out of Scope for This Milestone

- A formal evaluation harness or dataset to *measure* how often grounding validation catches real errors —
  Milestone 8.
- Any numeric/calibrated confidence score — Milestone 9; this milestone stays entirely within the qualitative
  Source Support model.
- A second, genuinely independent model/provider for grounding validation — noted as a limitation, not built;
  no concrete need has been demonstrated yet, and PRD §18 defers provider/model choice questions generally.
- Per-sentence or per-claim (as opposed to per-citation) grounding granularity — the existing `CitedChunkIds`
  is the right atomic unit to extend, per Milestone 6's own design; going finer-grained is a bigger change
  with no demonstrated need yet.
- Improving retrieval itself (query rewriting, re-ranking, a low-confidence-retrieval gate) — still deferred
  from Milestone 6, and not this milestone's job either.
- Any UI work (Milestone 10).
