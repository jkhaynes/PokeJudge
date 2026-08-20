# Milestone 8.5 Review — Evaluation Dataset Hardening

**Milestone reviewed:** Milestone 8.5 — Evaluation Dataset Hardening
**Plan reviewed against:** `.project-plans/milestone-8.5/plan.md`
**Also reviewed:** `implementation-summary.md`, `observed-limitations.md`, `source-coverage-analysis.md`,
`five-run-validation.md`, PRD §15 ("Evaluation dataset hardening (Milestone 8.5)"), PRD §14 roadmap
(Milestones 8.5 and 9), and the full commit history on `milestone/8.5-eval-dataset-hardening`
(`b19d8c0..HEAD`, 5 commits).

Build: `dotnet build` — 0 warnings, 0 errors. Tests: `dotnet test` — **219/219 passing.**

---

## ✅ Matches the Plan

The originally-scoped hardening work (plan steps 1–9, the two checkpoint commits `2c392f7`/`a8f4fb1`) is
solid and does what §15 asked for:

- **Dataset expanded 8 → 20 scenarios**, closing both previously-missing PRD categories (Attack Resolution,
  Timing Questions), with every new scenario's expected section(s) verified live via `dotnet run -- search`
  before being added — not guessed at. `EvalDatasetTests` structurally enforces the size range, category
  vocabulary, and the "every `RequiresOneClarification` scenario has a scripted answer" invariant.
- **Category normalization done and actually consumed** — `ScenarioCategories.Allowed`, `CategorySummary`,
  and `Program.cs`'s new per-category breakdown are exactly the "captured but never used" fix the plan called
  for, learning directly from Milestone 8's own `BranchGroup` mistake.
- **Original 8 scenarios' expected trajectories reviewed individually**, not blanket-trusted or blanket-kept
  — `deck-not-shuffled` and `special-condition` kept with cited live-search evidence; `spectator-badges`
  investigated via the new source-coverage classification rather than assumed. This is the milestone's
  central learning objective, done for real.
- **`--repeat` support** is a clean, minimal `EvalScenarioSelector` extension; `ScenarioEvalRunner` and
  `ScenarioEvalScorer` genuinely untouched by it (verified by reading both), exactly matching the plan's "no
  non-determinism logic in the scorer" requirement.
- **Infrastructure-failure handling** works as designed and was validated by a real, unplanned 12-in-a-row
  429 event (`observed-limitations.md` §2) — a stronger test than a deliberately engineered one.
- **`SourceCoverageFinding`/`SourceCoverageFindings`** were built as real, populated, tested data from day
  one, not a shape awaiting future use — the plan's step 5 explicitly asked for this pattern.
- **The multi-answer scripted-judge generalization** (`ScriptedAnswer` → `ScriptedAnswers`) was discovered as
  a real need during live testing (not speculative), root-caused correctly (the model was repeating one
  question, not asking a sequence — the fix was correcting the answer content, not adding rounds), and is
  unit-tested for the genuine multi-fact case it does serve.
- Every new fix across the whole branch (`late-to-round`, `deck-under-60`, `ace-spec-count`,
  `weakness-not-applied`, `supporter-twice`) followed the project's established evidence discipline: live
  question-text diagnosis before guessing, live re-verification after the fix, honest reporting when a fix
  didn't fully resolve (`ace-spec-count` needed three iterations; `mulligan-not-taken` still shows unresolved
  variability after its fix). This is good practice, consistently applied.

---

## 🚨 Must Fix

### 1. The zero-question-crash fix is a real, undisclosed scope expansion beyond the approved plan

**File/location:** `PokeJudge/Clarification/SystemPrompts.cs`, `PokeJudge/Clarification/ClarificationLoop.cs`,
`PokeJudge/StructuredState/ClarificationResult.cs`, `PokeJudge/Evaluation/ScenarioEvalScorer.cs`,
`PokeJudge/Evaluation/EvalScenario.cs` (commits `3eae3dc`, `ed68aee`).

**Problem:** The approved plan and PRD §15 both scope Milestone 8.5 as "a hardening pass over the existing
harness and dataset, not a new evaluation capability" (plan.md line 13; PRD.md line 348), explicitly listing
"the recurring insufficient-with-zero-clarifying-questions failure" as one of several **findings to keep
distinguishable and measure**, not fix (PRD.md lines 387–391). The plan's own step 3 goes further, explicitly
arguing *against* touching this: `drew-extra-card`'s "repeated crash is evidence the known
sufficiency-assessment bug persists, not evidence the expectation is wrong. Downgrading it to
`ExpectedToFailLoudly` would quietly convert a real regression signal into a tautology" (plan.md lines 83–85,
verbatim also in `EvalDataset.cs`'s own header comment, lines 39–45). The plan's explicit Out-of-Scope list
also states "Any change to `ScenarioEvalScorer`'s scoring criteria themselves" is out of scope (plan.md line
159).

Later work in this branch did exactly what both documents said not to: it changed `SystemPrompts.Judge` (a
system prompt used by both the eval harness *and* the interactive judge-facing console flow — this is a
production AI-behavior change, not an eval-only change), added a new `ClarificationResult.Rationale` field to
the structured-output schema the model must satisfy, added a new `ExpectedTrajectoryOutcome.ExpectedUnresolvable`
enum value, and added a new `ScenarioEvalScorer.ScoreExpectedUnresolvable` scoring branch — a direct scoring-
criteria change the plan explicitly disallowed. `missed-prize` was reclassified from `ExpectedToFailLoudly` to
this new outcome specifically because the fix eliminated the crash it existed to test for.

**Why it matters:** This isn't a quality problem with the fix itself — it's well-diagnosed (used the new
`Rationale` field to see the model's actual reasoning before acting), live-verified across 8+ real runs, and
honestly documented in `five-run-validation.md`. The problem is that it's a different *kind* of work than this
milestone was approved to do. PRD §15 frames the zero-question crash as something Milestone 9's calibration
analysis and future milestones are meant to reckon with as a known limitation, not something 8.5 was supposed
to eliminate. Fixing it here means Milestone 9's "confidence calibration" work will now be studying a
pipeline whose sufficiency-assessment behavior materially changed mid-stream, and the roadmap never named a
milestone for "fix known reasoning bugs found via eval" — this pulled that undefined future work forward
without an explicit decision to do so.

**Recommended direction:** Not to revert the fix — it's real, working, well-evidenced improvement. But the
scope decision should be made explicit rather than left implicit: either (a) formally amend
`.project-plans/milestone-8.5/plan.md` with an addendum documenting this deliberate scope expansion and why it
was judged worth doing now, or (b) split this work into its own follow-up entry (e.g. a "Milestone 8.6" or a
named addendum) so the roadmap accurately reflects that a production AI-behavior fix happened outside the
milestone that was supposed to only harden data collection. Either way, this should be a conscious decision
recorded in the plan, not something a reader discovers only by diffing the branch against PRD §15.

### 2. Three documents now contain stale, actively false claims about system behavior

**File/location:** `PokeJudge/Evaluation/EvalDataset.cs` lines 39–45; `.project-plans/milestone-8.5/observed-limitations.md`
lines 63–64; `.project-plans/milestone-8.5/implementation-summary.md` lines 14–15 and 72–75.

**Problem:**
- `EvalDataset.cs`'s header comment still says `drew-extra-card`'s "repeated crash is evidence the known
  sufficiency-assessment bug persists" — but live testing this session showed `drew-extra-card` no longer
  crashes at all (0/2 in the post-fix verification run).
- `observed-limitations.md` §3 states a regression check "confirmed no behavior change" for `drew-extra-card`
  and that "it still reproduces the known, unrelated zero-questions crash exactly as before" — also now false.
- `implementation-summary.md` states "No new NuGet packages. No changes to `Clarification/`, `Grounding/`,
  `Retrieval/`, `StructuredState/`, or `ScenarioEvalScorer.cs`" and "No change to `ScenarioEvalScorer`'s
  scoring criteria" — both now false; all of `Clarification/ClarificationLoop.cs`,
  `Clarification/SystemPrompts.cs`, `StructuredState/ClarificationResult.cs`, and `Evaluation/ScenarioEvalScorer.cs`
  were modified in this branch (see the finding above).
- `SourceCoverageFindings.cs`'s `missed-prize` entry still describes "this scenario's crash is so consistently
  reproducible" — `missed-prize` no longer crashes post-fix (reclassified to `ExpectedUnresolvable`); this one
  is a smaller staleness since the underlying source-gap classification itself is still accurate.

**Why it matters:** These are exactly the kind of claims a future developer (including a future milestone's
you) would read and trust without re-verifying — "no change to `ScenarioEvalScorer`" is a specific, checkable
claim now printed in a file that's supposed to be the milestone's accurate historical record. Milestone 9
explicitly builds on this dataset and its documented findings; inheriting a false "the crash still reproduces
exactly as before" belief could misdirect that work.

**Recommended direction:** Update all three documents to reflect what's actually true as of `HEAD`, or add a
clearly-dated addendum section to each (matching the pattern already used successfully in
`five-run-validation.md`'s "Follow-up: 2026-08-20" section) rather than leaving the original claims
uncorrected.

---

## ⚠️ Consider Improving

- **`Program.cs`'s eval banner still reads "Milestone 8 Scenario Evaluation"** (`Program.cs` line 588),
  unchanged since Milestone 8 despite this milestone's substantial harness rework. Cosmetic, but a
  one-line fix and mildly confusing to anyone running `evaluate` fresh.
- **`mulligan-not-taken` has three distinct, only-partially-understood live trajectories** (clean via
  `TCGTH-3.3.1`, a `PPG-4.2.1`-anchored path needing a second round with an unexpected Strong Source Support,
  and a recurring genuine retrieval miss reproducing the zero-question crash) per `five-run-validation.md`'s
  final follow-up section. This is honestly documented as open rather than papered over, which is the right
  call — flagging only so it isn't lost as a candidate for a future dedicated source-coverage investigation,
  per that document's own recommendation.
- **`ExpectedTrajectoryOutcome` now has two similarly-shaped "doesn't reach sufficiency, and that's expected"
  outcomes** (`ExpectedToFailLoudly` and `ExpectedUnresolvable`) with a subtle distinction (crashes vs. exhausts
  the turn cap) that a future reader may not immediately register as different. The naming and doc comments on
  both are already good; no action needed beyond awareness if a third such outcome is ever considered.
- **`RequiresOneClarification`'s name staying slightly inaccurate** (a scenario can now script more than one
  round) was already flagged and deliberately deferred in `implementation-summary.md` — still true, still a
  reasonable deferral, just noting it's still there.

---

## 🧪 Learning Observations

- **`observed-limitations.md` §1 and `five-run-validation.md`'s `deck-not-shuffled`/`special-condition` data
  are the clearest demonstration in the whole project so far of "a single observed run is not a stable
  ground-truth label."** Running `--repeat 3` on `deck-not-shuffled` produced three different outcomes in one
  command — this is worth re-running yourself and watching happen live, since reading about it and watching it
  happen land differently.
- **The `ace-spec-count` investigation (`five-run-validation.md` finding 5) is a strong worked example of
  "two different real follow-up questions across two fixes is itself evidence the topic is procedurally
  entangled, not a wording problem worth a third guess."** Worth reading end-to-end as a model for how to stop
  chasing a scripting fix once the evidence says the scenario itself is the interesting finding.
- **The zero-question-crash investigation is the best demonstration in this project so far of why a
  diagnostic field matters before a fix is attempted.** Before the `Rationale` field existed, six scenario
  categories reproduced a crash with literally zero information about why. After adding it, one crash
  recurrence (`mulligan-not-taken`, `five-run-validation.md`'s final section) came with an exact, readable
  explanation: *"none of the retrieved passages address mulligans... the passages instead discuss deck lists,
  playing multiple Supporter cards, and infinite loops."* That single sentence reclassified what had looked
  like a reasoning bug into a retrieval-quality problem — worth understanding as the general lesson, separate
  from whether fixing it here was in-scope (see Must Fix #1).
- **The `missed-prize` reclassification is a genuine, non-trivial example of a regression check being
  invalidated by a fix elsewhere in the system** — not a hypothetical caution from the plan, but something
  that actually happened and was caught by re-running the scenario rather than assumed. Worth understanding
  concretely: fixing bug A can silently break the test built specifically to detect bug A, and the only way to
  know is to re-run it.

---

## 🎯 Learning Objective Check

1. **What AI concept was this milestone intended to teach?** Evaluation-dataset curation as a discipline
   distinct from building the harness (Milestone 8); distinguishing model non-determinism from a wrong eval
   expectation; root-cause attribution in a RAG system (retrieval vs. corpus vs. reasoning); where
   non-determinism handling belongs in a layered system.

2. **Does the implementation expose that concept clearly?** Yes, and unusually well for the scoped work —
   `deck-not-shuffled`'s `--repeat 3` result, the `spectator-badges`/`deck-under-60` source-coverage
   classifications, and the `ace-spec-count`/`mulligan-not-taken` investigations are all concrete, readable
   demonstrations of exactly these distinctions, not just a claim that the harness supports them.

3. **What should the developer be able to explain after completing this milestone?** All of: why 8→20
   scenarios still isn't "enough" scenarios and what that limitation actually means for Milestone 9; how to
   tell non-determinism from a wrong label using `--repeat`; how to classify a failure's root cause using
   retrieved-chunk inspection; why `ScenarioEvalScorer` staying untouched by `--repeat` matters architecturally.
   Additionally, the developer should now also be able to explain *why* the sufficiency-assessment crash
   happened (a missing diagnostic field) and what changed to reduce it — real, valuable knowledge, just from
   work that extended past this milestone's original boundary (see Must Fix #1).

4. **Is any abstraction hiding something the developer should understand directly?** No — if anything, the
   opposite: this milestone repeatedly added visibility (question-text printing, the `Rationale` field) rather
   than hiding behavior. The one caution is that `Program.cs`'s eval loop is orchestration coupled to live
   async calls, consistent with the project's established practice of not unit-testing `Program.cs` directly —
   appropriate here, not a concern.

---

## 📋 Plan Completion

| Plan step | Status |
|---|---|
| 1. Expand `EvalDataset.cs` to ~20-30 scenarios | **Complete** — 20 scenarios, both missing categories covered, live-verified sections |
| 2. Normalize `Category`, add per-category summary | **Complete** — `ScenarioCategories`, `CategorySummary`, wired into `Program.cs` |
| 3. Review original 8 scenarios' expected trajectories | **Complete** — all four named scenarios investigated individually with documented rationale |
| 4. Add `--repeat` support | **Complete** — `EvalScenarioSelector`, tested, scorer untouched |
| 5. Add `SourceCoverageFinding` classification type | **Complete** |
| 6. Distinguish infrastructure failures | **Complete** — validated by a real 12-in-a-row 429 event |
| 7. Manually investigate & record source-coverage findings | **Complete** — 5 scenarios investigated (4 planned + `deck-under-60` added later), written up in `source-coverage-analysis.md` |
| 8. Run the expanded harness for real, incl. `--repeat` | **Complete**, and substantially exceeded — full five-run validation pass across all 20 scenarios (`five-run-validation.md`), well beyond the plan's minimum ask |
| 9. Update/add tests | **Complete** — 219/219 passing, includes tests added for work beyond the original plan (rationale-message tests, `ExpectedUnresolvable` scorer tests) |
| 10. Document findings | **Complete**, but see Must Fix #2 — some of that documentation is now stale relative to later work in the same branch |
| *(unplanned)* Fix the zero-question sufficiency-assessment crash | **Not in the plan** — see Must Fix #1. Real, well-executed work; a scope decision that should be made explicit rather than implicit. |

---

## Final Verdict

**Ready After Minor Fixes**

The originally-scoped hardening work is genuinely complete, well-tested, and does exactly what Milestone 8.5
was approved to do — this is not a quality problem. What keeps this from a clean "Ready to Complete" is
process, not code: real production AI-behavior and scoring-criteria changes were made that the approved plan
explicitly scoped out (Must Fix #1), and three documents now contain claims about system behavior that later
work in the same branch made false (Must Fix #2). Both are fixable without touching the underlying
engineering — update the plan/summary documents to honestly reflect what was actually built and why the scope
grew, and this milestone is done.
