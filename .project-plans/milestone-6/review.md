# Milestone 6 Review

**Milestone reviewed:** Milestone 6 — RAG
**Plan:** `.project-plans/milestone-6/plan.md`
**Branch:** `milestone/6-rag`
**Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
**Tests:** `dotnet test` — 108/108 passed at time of review; 108/108 after the fix below (no new tests
needed — the fix is a pure literal-to-constant change with no behavior difference).

**Update:** Consider-Improving item #1 (the duplicated `topK: 5` literal) has since been fixed — see its
note below.

## ✅ Matches the Plan

- **All "What We Will Build" items delivered**: `MockCorpus`/`PolicySnippet`/`MockScenario` and their
  tests are fully removed; `IRetriever`/`VectorStoreRetriever` wrap Milestone 5's retrieval behind a
  testable seam; `ClarificationLoop` retrieves every turn from a query that provably grows with confirmed
  facts; ruling generation is its own explicit step (`RulingGenerator`) producing a first-pass,
  model-assigned Source Support label.
- **Ruling generation genuinely separated from sufficiency**, matching PRD §11's architecture diagram —
  `ClarificationResult`/`ClarifyingQuestion` no longer carry a draft; `RulingGenerator` is a distinct class
  with its own system prompt and schema, called only after the loop reports sufficient.
- **Iterative retrieval verified with real evidence, not just claimed.** The Special Condition scenario in
  `observed-limitations.md` shows the turn-2 retrieved set genuinely differing from turn-1's after a
  confirmed fact was added (3 of 5 results changed) — independently reproducible by re-reading
  `RetrievalQueryBuilder.Build` and `ClarificationLoop.RunAsync`, which rebuilds the query from
  `state.ConfirmedFacts` at the top of every turn.
- **No material inference preserved.** `PromptBuilder.BuildRulingPrompt` explicitly labels hypotheses "not
  confirmed -- must never be used to support the ruling," and `SystemPrompts.RulingGeneration` repeats the
  instruction. The real Special Condition run shows this working correctly: two plausible hypotheses were
  extracted and correctly excluded from the final ruling's grounding.
- **Fail-visibly discipline (PRD §9) genuinely exercised, not just asserted.** The real missed-Prize-card
  run reproducibly caused the model to return `isSufficient: false` with zero questions, and the existing
  Milestone 2 guard (`RunAsync_InsufficientWithoutQuestions_ThrowsRatherThanLoopingSilently`'s production
  counterpart) caught it with a clear, loud exception rather than any silent degradation.
- **Reuses Milestone 4-5's abstractions correctly.** `VectorStoreRetriever` is a thin wrapper with no new
  embedding or similarity logic; `InMemoryVectorStore`/`IEmbeddingClient` are unchanged.
- **Tests are meaningful, not tautological.** `ClarificationLoopTests` specifically asserts the retrieval
  query text grows and differs between turns (not just that retrieval was called); `RulingGeneratorTests`
  asserts the actual prompt content sent to the model, not just that a result comes back.
- **Turn-cap-exhausted path correctly refuses to rule.** `Program.cs` prints an explicit
  "no ruling produced" message and returns before ever calling `RulingGenerator`, satisfying PRD §9 — this
  path was not exercised in the real-data runs captured in `observed-limitations.md`, but is covered by
  `ClarificationLoopTests.RunAsync_NeverSufficientWithinTurnCap_ReturnsTurnCapExhausted` plus direct
  reading of the `Program.cs` branch.

## 🚨 Must Fix

None.

## ⚠️ Consider Improving

- ~~**`topK: 5` is duplicated as a literal** in `ClarificationLoop`'s default constructor parameter and in
  `Program.cs`'s final-retrieval call. Not a correctness bug today (both happen to agree), but Milestone
  5's own review flagged the identical pattern for the embedding model/dimensionality and fixed it with a
  single constant.~~ **Fixed.** Added `ClarificationLoop.DefaultTopK` (`= 5`) as the single source of truth,
  used as the constructor's default parameter value; `Program.cs`'s final-retrieval call now references
  `ClarificationLoop.DefaultTopK` instead of a second `topK: 5` literal. The two call sites can no longer
  silently drift apart if top-K is ever tuned. Re-verified: `dotnet build` clean (0 warnings/errors),
  `dotnet test` still 108/108 passing, no behavior change.
- **`ClarifyingQuestion.RelatedChunkId` is not validated against the chunks actually retrieved that turn.**
  Nothing checks that the model's cited chunk ID exists in the retrieved set it was just shown (a
  hallucinated or stale ID would pass through silently). This isn't a regression — Milestone 2's original
  `RelatedSnippetId` was equally unvalidated — but it's now checking against real, retrieved data instead
  of a fixed mock list, and PRD §7's Milestone 7 write-up explicitly lists "citation-ID existence" as a
  candidate *deterministic* grounding check. Good candidate for Milestone 7, not a fix-now item.
- **`RulingResult.CitedChunkIds` is similarly unvalidated** against the chunks supplied in that call's
  prompt — same category of issue, same appropriate deferral to Milestone 7's citation-coverage checks.

## 🧪 Learning Observations

- **The low-confidence-retrieval failure manifested differently than the plan anticipated, which is itself
  worth noting.** The plan expected weak retrieval to surface as "a plausible-but-not-quite-right chunk" or
  a miss (Milestone 5's framing). What was actually observed is one step earlier and more structural: weak,
  topically-adjacent retrieval caused the *sufficiency assessment itself* to produce a malformed response
  (insufficient with zero questions) rather than reaching generation with a weak `Insufficient`-labeled
  ruling. That's a more useful, more concrete finding for motivating Milestone 7 than the originally
  anticipated failure mode — the gap isn't just "the final answer might be shaky," it's "the model can't
  always articulate what's missing when nothing retrieved is on-target."
- **A real Partial classification, self-limited by the model without being told the answer.** The Special
  Condition scenario's ruling correctly identified that the retrieved passages establish what's public
  information but don't prescribe a resolution procedure — a nuanced, correct-seeming distinction reached
  purely from the model's own reading of the rubric in `SystemPrompts.RulingGeneration`, with nothing in
  the code checking it. This is good, concrete evidence for the "Source Support is model judgment, not yet
  validated" learning point — the label looked reasonable *and* the milestone is right that reasonable
  isn't the same as verified.
- **Context-window budgeting (named in PRD's Milestone 6 learning objectives) wasn't concretely exercised.**
  Retrieved-chunk text volume at `topK: 5` over this corpus never approached a size where budgeting would
  matter, so nothing in this milestone's real runs demonstrates that concept directly — worth knowing this
  is still a conceptual gap, not something to manufacture a fix for at this corpus scale.
- **The turn-cap-exhausted / no-ruling-produced path is implemented and unit-tested but wasn't triggered in
  any of the real manual runs** documented in `observed-limitations.md` (every real run either reached
  sufficiency or crashed on the malformed-response guard first). Worth deliberately forcing a scenario that
  the judge simply can't clarify well enough to see this path fire for real, if that evidence is wanted.

## 🎯 Learning Objective Check

1. **What AI concept was this milestone intended to teach?** Combining retrieval and generation end-to-end;
   iterative retrieval as a structural loop property, not a one-shot step; a first-pass, unvalidated
   Source Support label as an output of generation.
2. **Does the implementation expose that concept clearly?** Yes, with real evidence: a genuine before/after
   retrieved-chunk-set comparison across turns, a Strong ruling on well-covered material, and a Partial
   ruling the model reasoned its way to rather than defaulted to.
3. **What should the developer be able to explain after completing this milestone?** Why ruling generation
   is a separate model call from sufficiency assessment (not the same call doing both); how the retrieval
   query text is constructed and why it grows; what a first-pass, unvalidated Source Support label actually
   is versus a checked one; and the concrete, reproduced failure mode of weak retrieval producing a
   malformed sufficiency response rather than a graceful "I don't know."
4. **Is any abstraction hiding something the developer should understand directly?** No. `IRetriever` is a
   one-method seam whose only job is testability (mirrors `ILlmClient` exactly); `Program.cs` prints the
   retrieved chunks, scores, assessment outcome, confirmed facts/hypotheses, and full ruling at every step,
   so nothing about what the model was shown or produced is hidden from the console output.

## 📋 Plan Completion

| Step | Status |
|---|---|
| 1. Delete `MockCorpus`/`PolicySnippet`/`MockScenario`/`MockCorpusTests` | Complete |
| 2. Add `IRetriever` + `VectorStoreRetriever` + `StubRetriever` | Complete |
| 3. Add `RetrievalQueryBuilder` | Complete |
| 4. Rewrite `PromptBuilder.BuildSufficiencyPrompt` for retrieved chunks | Complete |
| 5. Simplify `ClarificationResult`/`ClarifyingQuestion`, update schema + `SystemPrompts.Judge` | Complete |
| 6. Rewrite `ClarificationLoop.RunAsync` for per-turn retrieval | Complete |
| 7. Add `RulingResult` + `SourceSupport` enum + schema | Complete |
| 8. Add `RulingGenerator` + `SystemPrompts.RulingGeneration` + `BuildRulingPrompt` | Complete |
| 9. Wire console default flow (free-text entry, retrieval visibility, final ruling print) | Complete |
| 10. Run real end-to-end flow against real corpus for 3 scenario types | Complete — well-covered success, multi-turn re-retrieval, and a real low-confidence failure all captured |
| 11. Update tests | Complete |
| 12. Document observed findings | Complete, and the low-confidence finding is more specific than the plan anticipated |

## Final Verdict

**Ready to Complete**

No Must Fix items. The implementation matches the approved plan closely, correctly separates ruling
generation from sufficiency assessment per PRD §11's architecture, and produced real (not staged) evidence
for every core learning objective — including a low-confidence-retrieval failure mode that is more precise
and more useful than what the plan anticipated. Of the two Consider-Improving items, the duplicated `topK`
literal is now fixed (`ClarificationLoop.DefaultTopK` is the single source of truth); the unvalidated
chunk-ID citations item remains open by design, explicitly and correctly scoped to Milestone 7 rather than
something this milestone should have fixed.
