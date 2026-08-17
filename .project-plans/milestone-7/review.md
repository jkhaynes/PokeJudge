# Milestone 7 Review

**Milestone reviewed:** Milestone 7 — Citations and Grounding
**Plan:** `.project-plans/milestone-7/plan.md`
**Branch:** `milestone/7-citations-grounding`
**Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
**Tests:** `dotnet test` — 153/153 passed at time of review; 154/154 after the fix below (1 new test).

**Update:** the Must Fix item below has since been fixed — see its note.

## ✅ Matches the Plan

- **All "What We Will Build" items delivered**: a new `Grounding/` module (matching PRD §11's diagram as a
  distinct third box, correctly kept separate from `Clarification/`), three deterministic checks, a
  structured per-citation LLM classification step, and a pure `SourceSupportAssigner` combinator implementing
  PRD §8's rubric as code.
- **`SourceSupportAssigner` correctly reasons over the ruling's own `CitedChunkIds`, not just the model's
  grounding-response `Citations` list.** A citation the model drops from its classification response is
  looked up via `citedChunkIds.Select(id => supportByChunkId.TryGetValue(...) ? ... : Unsupported)` and
  defaults to `Unsupported` rather than being silently skipped — this is exactly right and a meaningfully
  careful design choice, independently verified by re-deriving it: without this, a dropped classification
  would vacuously pass.
- **`AllCitationsExist` correctly treats an empty citation list as failing, not vacuously passing** — a
  ruling with zero citations has no evidentiary support, and the deterministic check reflects that rather
  than a literal (and wrong) reading of "does every ID in an empty list exist."
- **`BuildGroundingValidationPrompt` deliberately excludes the model's own `SourceSupport`/`SourceSupportRationale`**
  from the validation prompt — confirmed by reading the method — so the grounding-validation call isn't
  anchored on the very label it's supposed to check independently. Also correctly sends only the *cited*
  chunks, not the full retrieved set, preventing the validator from justifying a claim using material the
  ruling never actually cited.
- **Console output matches PRD §10's ordering** (recommendation, then Source Support prominently, then
  explanation/repair/penalty/sources) and shows both labels side by side plus the per-citation breakdown, so
  divergence is visible rather than buried — directly satisfies the plan's console-wiring requirement.
- **Required deliverables present and substantive, not perfunctory**: `grounding-analysis.md` gives a real
  deterministic-vs-model-judgment breakdown and a concrete (not just asserted) same-model-self-validation
  discussion; `observed-limitations.md` documents three genuine real-data runs, including a divergence case
  that contradicts what the plan predicted it would find (see Learning Observations).
- **Tests are meaningful, not tautological.** `SourceSupportAssignerTests` is table-driven across the full
  rubric including the dropped-citation edge case; `GroundingValidatorTests` asserts actual prompt content
  reaches the model (mirroring `RulingGeneratorTests`' established pattern), not just that a result comes
  back; the final coverage-review pass correctly caught that `GroundingAssessment`'s real JSON deserialization
  (via `StructuredResponseParser`) was untested and added it.
- **Nothing from later milestones was built**: no eval harness, no numeric confidence, no second
  model/provider, no per-sentence granularity, no retrieval improvements — all correctly left out per the
  plan's Out-of-Scope section.

## 🚨 Must Fix

- ~~**`SourceSupportAssigner.Assign` will throw an unhandled, unlabeled exception if the grounding-validation
  model's response contains a duplicate `chunkId` in its `citations` array.**~~ **Fixed.** Added an explicit
  duplicate-`ChunkId` check (grouping `assessment.Citations` and detecting any group with more than one
  entry) before building the lookup dictionary, throwing a named `InvalidOperationException` that lists the
  duplicated ID(s) — the same "fail loudly, clear message" pattern used elsewhere in this codebase for
  malformed structured output (e.g., Milestone 2's "insufficient with zero questions" guard), rather than
  silently resolving the ambiguity by picking one entry. Considered and rejected two silent alternatives
  first: taking the first duplicate entry (risks silently picking a favorable classification over a
  contradicting unfavorable one) and taking the worst of the duplicates (still quietly absorbs a malformed
  response instead of surfacing it). Added
  `Assign_ModelClassifiesTheSameChunkIdTwice_ThrowsNamingTheDuplicate`, asserting both the exception type and
  that the message names the duplicated ID. Re-verified: `dotnet build` clean (0 warnings/errors), `dotnet
  test` 154/154 passing (1 new test), no behavior change on any non-duplicate path.

## ⚠️ Consider Improving

- **`DeterministicGroundingChecks.RetrievalNonEmpty` and `FactsWereSufficient` are, in the actual console
  flow, structurally close to always-true guarantees, not real gates.** `GroundingValidator` is only ever
  invoked in `Program.cs` after `outcome.Sufficient` is confirmed true (making `FactsWereSufficient` trivially
  true every real run, exactly as the code comment already acknowledges), and `finalChunks` comes from a
  retrieval call that only returns empty if the entire corpus is empty (already gated earlier in `Program.cs`
  with its own error message). Neither check has a realistic path to actually firing against real usage today
  — only `AllCitationsExist` and the per-citation LLM classification have realistic paths to catching
  something in the current console flow. Not a defect (the plan explicitly frames `FactsWereSufficient` as a
  defensive, explicit criterion rather than one expected to fire), but worth being clear-eyed about which of
  the four inputs to `SourceSupportAssigner` are actually exercised by real usage versus present mainly for
  future-proofing/unit testability.
- **No test constructs a `GroundingAssessment` with more than one citation at mixed support levels** (e.g.,
  one `ExplicitSupport` and one `Interpretation` in the same response) to confirm the `Partial` branch is
  reached via the multi-citation `Any(...)` path rather than only the single-citation case already tested.
  Low risk given the implementation is a straightforward `Any()`, but it's the one rubric interaction the
  current table-driven tests don't directly exercise.

## 🧪 Learning Observations

- **The Special Condition scenario's divergence manifested differently than the plan predicted, which is
  itself worth noting** — the plan expected to observe the model *over-calling* `Strong` on a citation that's
  actually only interpretive; what was actually observed is a more structural and arguably more instructive
  gap: every individual citation was genuinely `ExplicitSupport` for its own narrow claim, but the
  *combinator* still reached `Strong` because nothing in the per-citation scheme asks whether the
  recommendation's synthesis across citations requires judgment. This is a better, more specific finding than
  what was anticipated for motivating future grounding-granularity work — the same pattern Milestone 6's own
  review noted about its low-confidence-retrieval finding landing one step earlier/more structurally than
  expected. Worth reading `observed-limitations.md` §2 in full.
- **The missed-Prize scenario's re-run is a clean, valuable negative result.** Retrieval improved
  dramatically (0.80 directly on-topic vs. ~0.75 topically-adjacent in Milestone 6) and the scenario still
  failed identically at the sufficiency-assessment step, never reaching this milestone's new code at all. This
  cleanly separates "retrieval coverage" from "sufficiency-assessment robustness" as two distinct problems —
  good evidence that Milestone 6's original diagnosis was only half the story.
- **Source-conflict detection has literally never fired on a real positive case** across all real runs
  performed (Milestone 6's and this milestone's). This is honestly reported in `observed-limitations.md`
  rather than left unstated, which matters: an implemented-but-never-triggered check is easy to mistake for a
  validated one.

## 🎯 Learning Objective Check

1. **What AI concept was this milestone intended to teach?** Grounding validation and Source Support
   assignment as an explicit, separate step from generation; the distinction between citation *existence*
   (deterministic) and citation *support* (model judgment); why same-model self-validation is not independent
   verification.
2. **Does the implementation expose that concept clearly?** Yes, and with unusually strong real evidence —
   the console prints both labels side by side every run, so agreement and disagreement are both directly
   visible, and a real disagreement was actually captured and analyzed rather than merely being a
   theoretical possibility the architecture allows for.
3. **What should the developer be able to explain after completing this milestone?** Why `SourceSupportAssigner`
   is plain deterministic code rather than another model call; why an empty or dropped citation defaults to
   the unfavorable outcome rather than a vacuous pass; concretely, using the Special Condition case, why
   per-citation grounding checks can miss whole-ruling reasoning gaps; and why sharing one `ILlmClient`
   between `RulingGenerator` and `GroundingValidator` means the validation is not independent, with a specific
   mechanism for how that could let an error through undetected.
4. **Is any abstraction hiding something the developer should understand directly?** No. `GroundingValidator`
   is a thin orchestrator over inspectable pieces; `Program.cs` prints every deterministic flag, the raw
   per-citation classification, and both Source Support labels, so nothing about the validation's reasoning is
   hidden from the console output.

## 📋 Plan Completion

| Step | Status |
|---|---|
| 1. Add `Grounding/DeterministicGroundingChecks.cs` | Complete |
| 2. Add `Grounding/CitationGroundingCheck.cs` (enum, records, schema) | Complete |
| 3. Add `SystemPrompts.GroundingValidation` + `PromptBuilder.BuildGroundingValidationPrompt` | Complete |
| 4. Add `Grounding/GroundingValidator.cs` | Complete |
| 5. Add `Grounding/SourceSupportAssigner.cs` | Complete — duplicate-citation-ID crash path found in review, now fixed (see Must Fix) |
| 6. Wire `Program.cs` (both labels, citation breakdown) | Complete |
| 7. Run real end-to-end flow for 3 scenarios | Complete — agreement case, a real and instructive divergence case, and the re-run missed-Prize negative result |
| 8. Update/add tests | Complete |
| 9. Write `grounding-analysis.md` | Complete |
| 10. Document observed findings in `observed-limitations.md` | Complete |

## Final Verdict

**Ready to Complete**

The implementation is architecturally sound, stays tightly within Milestone 7's scope, and produced genuinely
strong, real evidence for its core learning objective — including a divergence case that's more interesting
than what the plan anticipated finding. The one Must Fix item (`SourceSupportAssigner`'s unhandled
duplicate-citation-ID crash path) has since been fixed with a clear, named exception and a regression test;
build and tests are clean (154/154). The remaining Consider-Improving items are genuinely optional and don't
block calling this milestone done.
