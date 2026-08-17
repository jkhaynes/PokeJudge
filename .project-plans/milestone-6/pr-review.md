# Milestone 6 — PR Review

## PR Review Summary

- **Milestone:** Milestone 6 — RAG
- **Current branch:** `milestone/6-rag`
- **Base branch:** `master`
- **What changed:** Retires Milestone 2's fixed `MockCorpus` and rebuilds the clarification loop as the
  real retrieve → assess → clarify → re-retrieve loop from PRD §11. Adds `IRetriever`/`VectorStoreRetriever`
  (thin wrapper over Milestone 4-5's embedding client + vector store) and `RetrievalQueryBuilder` (pure,
  query grows with confirmed facts). `ClarificationLoop` now retrieves every turn instead of reasoning over
  a static corpus. Ruling generation becomes its own explicit step (`RulingGenerator` + `RulingResult` +
  `SourceSupport` enum), producing a first-pass, model-assigned Source Support label. Console flow rewired
  for free-text scenario entry with per-turn retrieval visibility. All changes are currently uncommitted in
  the working tree (no commits yet ahead of `master`).
- **Overall impression:** Clean, well-scoped implementation that matches its own plan closely. The
  separation of sufficiency and ruling generation into distinct calls is done correctly and consistently;
  iterative retrieval is real (query text genuinely grows, verified by test and by the real-data run in
  `observed-limitations.md`); the "no material inference" discipline from Milestone 2 is preserved through
  the new ruling-generation path. No correctness or safety blockers found.
- **Build/test status:** `dotnet build` — succeeded, 0 warnings, 0 errors (verified independently).
  `dotnet test` — 108/108 passed (verified independently, matches the implementation summary's reported
  count).

**Update:** the Minor Issue below (redundant final retrieval call) has since been fixed — see its note.

## 🚫 Blockers

None.

## ⚠️ Major Issues

None.

## 🔎 Minor Issues

- ~~**`Program.cs`'s final pre-ruling retrieval call is provably redundant on every "reached sufficiency
  normally" path.**~~ **Fixed.** `Program.cs` now captures the retrieved chunks from the `onAssessment`
  callback into a local (`lastRetrievedChunks`), updated every turn, and reuses that captured value for
  `RulingGenerator.GenerateAsync` instead of issuing a second, guaranteed-identical `retriever.RetrieveAsync`
  call — since `ClarificationLoop.RunAsync` returns `SufficientAt` immediately after a turn's assessment
  reports sufficient (no further facts are added that turn), the confirmed facts at that point are always
  identical to what the loop's own last turn already retrieved against. `RetrievalQueryBuilder.Build` and the
  now-unused `finalQuery` construction are removed from `Program.cs`; `RetrievalQueryBuilder`'s own class and
  tests are untouched (it's still exercised by `ClarificationLoop` every turn). Re-verified: `dotnet build`
  clean (0 warnings/errors), `dotnet test` still 108/108 passing. Console output changes trivially: the
  chunk count/scores for the sufficient turn are already printed once by `onAssessment`, so the separate
  `[Final retrieval: N chunk(s)]` block (which would have printed the identical list a second time) is gone
  rather than duplicated.

## 💬 Review Notes

- Independently re-derived the same "Consider Improving" items the milestone's own `review.md` already
  flagged, and confirmed the `topK` duplication item is genuinely fixed (`ClarificationLoop.DefaultTopK` is
  the sole source of truth, referenced by both `ClarificationLoop`'s constructor default and `Program.cs`'s
  final-retrieval call).
- `ClarifyingQuestion.RelatedChunkId` and `RulingResult.CitedChunkIds` remain unvalidated against the chunk
  IDs actually supplied that call — confirmed this is the same pre-existing gap from Milestone 2
  (`RelatedSnippetId` was equally unvalidated against `MockCorpus.Snippets`), now just checked against real
  retrieved data. Correctly scoped to Milestone 7 (citation-ID existence is explicitly listed there as a
  candidate deterministic check) rather than something this milestone should have fixed — agree with the
  milestone's own review on this.
- `StructuredResponseParser`'s new `JsonStringEnumConverter()` will throw `JsonException` (not silently
  default/null) if the model ever emits a `sourceSupport` value outside the three enum names — consistent
  with the project's established "fail loudly" pattern, and low-risk since the schema itself constrains the
  model to the three exact enum strings. No test exercises the invalid-enum-value case specifically, but
  this is a low-value test to add given the existing malformed-JSON coverage already proves the parser fails
  loudly rather than silently.
- `GameState.AddConfirmedFacts`/`AddHypotheses` still don't de-duplicate (pre-existing since Milestone 2, not
  touched by this diff) — out of scope for this review, noted only for completeness.

## 🤖 AI-Specific Review

- **Prompt construction correctly reflects the two-call architecture.** `PromptBuilder.BuildSufficiencyPrompt`
  and `BuildRulingPrompt` both clearly separate confirmed facts (usable) from hypotheses (visible but
  explicitly barred from supporting a conclusion), and both instruct the model to reason only over the
  supplied retrieved passages — matching PRD §8's "no material inference" and "retrieval grounding"
  requirements. `SystemPrompts.Judge` and `SystemPrompts.RulingGeneration` are appropriately distinct: the
  former explicitly forbids producing a ruling, the latter restates the PRD §8 Strong/Partial/Insufficient
  rubric directly rather than leaving the label to the model's undirected judgment.
- **No hidden reliance on undocumented model behavior found.** The loop's control flow (retrieve → assess →
  ask/extract → loop, or return sufficient) is fully driven by the structured `ClarificationResult` fields,
  not by parsing free text or inferring intent from prose. The `RunAsync_InsufficientWithoutQuestions_...`
  guard means a malformed model response (insufficient + zero questions) fails loudly rather than the loop
  silently retrying or guessing a question — this was exercised for real against the live corpus per
  `observed-limitations.md`, not just asserted in a unit test.
- **Source Support is exactly as advertised: a first-pass, unvalidated model label.** Nothing in this diff
  checks `RulingResult.SourceSupport` or `SourceSupportRationale` against retrieval scores, citation
  coverage, or confirmed-fact completeness — consistent with the plan's explicit scoping of that validation
  to Milestone 7. This is a correctly-labeled, intentional limitation, not a defect.
- **No low-confidence-retrieval gate.** Consistent with PRD §18's explicit deferral, no similarity threshold
  or minimum-passage-count check exists. The real observed failure mode (weak retrieval → malformed
  `isSufficient:false` with no questions → thrown exception) is the correct "fail visibly" behavior given
  that gap, not a silent degradation.

## 🧪 Test Review

- **Coverage is meaningful, not tautological.** `ClarificationLoopTests` specifically asserts the retrieval
  query text differs and grows across turns (`RunAsync_ReRetrievesEachTurn_...`), not just that
  `IRetriever.RetrieveAsync` was called. `RulingGeneratorTests` asserts actual prompt content (scenario text,
  confirmed facts, chunk IDs, chunk text) reaches the model, not just that a result comes back.
  `VectorStoreRetrieverTests` verifies the embed-then-search delegation and topK respect using a real
  `InMemoryVectorStore` with a stub embedding client — an appropriate integration-style test for a thin
  wrapper class. `RetrievalQueryBuilderTests` covers the pure function directly (no facts, some facts,
  growth) — correctly scoped as deterministic unit tests needing no stubs.
- **Missing coverage:** none rises to "should have been written." The malformed-enum-value deserialization
  case (noted above) is the only gap identified, and it's low-value given adjacent malformed-JSON coverage
  already proves the fail-loudly behavior at the parser level.
- **Build/test results:** `dotnet build` clean; `dotnet test` 108/108 passing — independently reproduced.
- **Manual AI experiments:** `observed-limitations.md` documents three real runs against the live 258-chunk
  corpus (clean single-turn success, a genuine multi-turn re-retrieval case with a measured chunk-set delta,
  and a reproducible low-confidence failure) — appropriate and sufficient manual validation for this
  milestone; a formal eval harness is correctly out of scope until Milestone 8.

## 📦 Scope Check

- **Does this branch correspond to the current milestone?** Yes — every changed/added file maps directly to
  Milestone 6's plan (retrieval seam, query builder, loop rebuild, ruling generator, console wiring, tests,
  `.project-plans/milestone-6/` docs).
- **Does the diff contain only work appropriate to this milestone?** Yes.
- **Did unrelated changes get mixed into the branch?** No unrelated files or changes found in the diff
  against `master`.
- **Did it implement anything from future milestones?** No. Explicitly and correctly avoided: no
  criteria-based Source Support validation, no claim-level citation grounding, no similarity-threshold gate,
  no eval harness — all correctly deferred per the plan's "Out of Scope" section.
- **Did it introduce unnecessary architecture or dependencies?** No. `IRetriever` is a single-method seam
  matching the existing `ILlmClient` pattern; `VectorStoreRetriever` adds no new logic over Milestone 4-5's
  existing embedding/vector-store code; no new NuGet packages or projects.

## Final Verdict

**Approve With Minor Comments**

No blockers or major issues. The implementation is correct, stays tightly within Milestone 6's scope, and
is backed by real (not staged) evidence for its core learning objectives. The one minor comment (the
provably redundant final retrieval call before ruling generation) is a cost/efficiency observation, not a
correctness defect, and easy to address in a follow-up if desired. The remaining open items (unvalidated
chunk-ID citations, no low-confidence-retrieval gate) are correctly and explicitly scoped to Milestone 7,
not this PR.
