# Milestone 9 Review — Confidence Calibration and Reliability

**Milestone reviewed:** Milestone 9 — Confidence Calibration and Reliability
**Plan reviewed against:** `.project-plans/milestone-9/plan.md`
**Also reviewed:** `implementation-summary.md`, `calibration-analysis.md` (§§1–10), the full working-tree diff
(`git diff --stat` against `master` — no commits made yet on this branch), and the underlying code
(`Reliability/`, `Evaluation/ScenarioEvalRunner.cs`, `Evaluation/ScenarioTrajectory.cs`,
`Clarification/PromptBuilder.cs`/`SystemPrompts.cs`, `AI/CallCountingLlmClient.cs`, `Program.cs`).

Build: `dotnet build` — 0 warnings, 0 errors. Tests: `dotnet test` — **251/251 passing.**

---

## ✅ Matches the Plan

- **`ConfidenceEstimator` built exactly as designed**: a separate LLM call, mirroring `GroundingValidator`'s
  shape, deliberately blind to both the grounding result *and* the ruling's own prior `SourceSupport` label
  (a design decision sharpened mid-implementation, beyond what planning alone specified, and correctly locked
  in with a dedicated test —
  `PromptBuilderTests.BuildConfidenceEstimationPrompt_NeverIncludesTheRulingsOwnSourceSupportLabelOrRationale`).
- **`CalibrationAnalysis` is exactly the kind of pure, deterministic module the plan called for** — `Bucket`,
  `BrierScore`, `ExpectedCalibrationError`, `BucketsSupportFineGrainedEce`, `ExcludeKnownIssues` are all
  independently testable and independently tested (23 cases), no LLM calls, no hidden state.
- **The sample-size-adequacy check is real, not decorative.** `BucketsSupportFineGrainedEce` was live-confirmed
  to correctly refuse a fine-grained ECE at every real sample size tried today (1, 11, 40, 49 observations) —
  the milestone's central "recognize when a dataset can't support a statistic" lesson is demonstrably working,
  not just asserted.
- **`ScenarioEvalScorer`'s criteria and `ClarificationLoop` are genuinely untouched**, confirmed via
  `git diff --stat` — this milestone consumes `AllPassed` as ground truth exactly as scoped, it doesn't
  redefine it.
- **The live experiment happened for real, repeatedly, and every claim in `calibration-analysis.md` traces to
  an actual command run** — no synthetic or assumed data anywhere in the four data-gathering passes.
- **Two real bugs were found and fixed during the live runs, both squarely in scope**: the
  `TaskCanceledException` infrastructure-handling gap (found 3×, fixed after it started blocking the plan it
  was supposed to enable) and the `RunCalibration` empty-filtered-list crash (`ArgumentException` from
  `BrierScore` on zero observations). Both fixes are narrow, match existing established patterns exactly
  (extending the `HttpRequestException` catch; an explicit zero-count guard), and both were exercised for real
  afterward — the `TaskCanceledException` fix caught a real recurrence gracefully instead of crashing.
- **The final test-coverage-review discipline caught a real gap**: the initial Red step missed that this
  codebase has an established convention of testing every `PromptBuilder.Build*Prompt` method; the coverage
  pass caught it and added 5 tests, including the one that locks in the "blind to Source Support" design
  decision.
- **The product decision is explicit, evidence-gated, and reasoned rather than asserted** — Source Support
  stays the sole judge-facing signal, and the write-up's reasoning evolved honestly as more data came in
  ("not enough evidence" → "evidence points toward real overconfidence") rather than being decided in advance
  and then rationalized.
- **The §10 investigation into the 6 zero-yield scenarios is genuinely excellent unscheduled work** — done
  entirely from existing evidence (no quota spent), correctly separated infrastructure-failure noise from
  genuine model-behavior data before drawing any conclusion, and honestly isolated the 2 of 6 that don't fit
  the explained pattern rather than forcing all 6 into one tidy story.

---

## 🚨 Must Fix

### 1. `CalibrationObservation.Category` and `.Criteria` are captured but never consumed — the exact anti-pattern this project has explicitly flagged before — ✅ RESOLVED

**File/location:** `PokeJudge/Reliability/CalibrationAnalysis.cs` lines 11–16 (the record definition);
`PokeJudge/Program.cs` line 858 (where both fields are populated); no location exists where either is read
back out.

**Problem:** `CalibrationObservation.Category` (line 13) is populated on every observation
(`Program.cs:858`) but never read anywhere — `CalibrationAnalysis.cs` has zero references to `.Category`, and
`PrintCalibrationReport` never groups or filters by it. `CalibrationObservation.Criteria` (line 16) is
similarly populated but never read — grep across the whole repository for `.Criteria` finds exactly two hits:
the population site and the unrelated `ScenarioEvalReport.Criteria` field it's copied from. Nothing in
`PrintCalibrationReport`, `CalibrationAnalysis`, or anywhere else ever displays, filters, or analyzes an
observation's `Criteria` breakdown.

This is the *exact* pattern this project has already named and explicitly guarded against twice: Milestone 8's
`BranchGroup` field (removed after its own PR review flagged it as "captured but never consumed"), and
Milestone 8.5's `EvalDataset.cs` header comment citing that exact lesson as the reason
`SourceCoverageFinding`/`SourceCoverageFindings` were "built as actual, populated, tested data from the start,
not a shape awaiting future use." Milestone 9 reproduces the same mistake in a new field.

**Why it matters:** `Criteria` isn't a stray unused field — the record's own doc comment (lines 6–10) states
its purpose explicitly: "a miscalibration finding should be traceable to which criterion failed... not just
flagged as 'wrong.'" `implementation-summary.md`'s Intentional Limitations section repeats this claim ("each
`CalibrationObservation` now also carries the full `Criteria` breakdown for manual, narrative interpretation").
But nothing in the actual implementation ever surfaces that breakdown for a human to interpret. The one place
`calibration-analysis.md` actually traces a miscalibration to its cause (`drew-extra-card`, §8) did so via a
completely separate, freshly-run `evaluate --only` command — not by reading `observation.Criteria` from the
already-captured calibration data. The infrastructure built specifically to enable this kind of diagnosis sits
unused while the one diagnosis that did happen worked around it.

**Recommended direction:** Either (a) print each observation's failing criteria (or a compact summary of
which ones failed) in `calibrate`'s console output, and/or (b) add a `CalibrationAnalysis`-level grouping
(e.g., "of the 13 incorrect observations, N failed retrieval, M failed materiality, ...") the way
`CategorySummary` already does for `evaluate`'s category breakdown — the precedent for this exact kind of
aggregation already exists in this codebase. `Category` should similarly get a per-category
bucket/Brier breakdown, or be removed from the record if it's genuinely not needed.

**Resolution:** Took direction (b), mirroring `CategorySummary`'s exact established pattern (group by
first-seen order, not alphabetized). Added `CalibrationAnalysis.SummarizeByCategory` (returns a
`CategoryCalibrationSummary` per category: count, mean predicted probability, observed correct rate) and
`CalibrationAnalysis.SummarizeCriterionFailures` (returns a `CriterionFailureCount` per criterion name,
counting only failures from incorrect observations). Both are pure, deterministic, and test-first
(7 new unit tests — empty-input, single/multiple-category grouping, correct-observations-excluded-from-failure-counts,
and multi-observation aggregation cases). Wired into `PrintCalibrationReport` (`Program.cs`), so every
`calibrate` run now prints a "By category" breakdown and a "Criterion failures among the N incorrect
observation(s)" breakdown alongside the existing Brier score and bucket table — the data these two fields
were always meant to support is now actually surfaced, not just captured. Full test suite: 258/258.

### 2. Plan step 6 / "What We Will Build" item 4 — comparing confidence against the pipeline's other reliability signals — was never executed — ⚠️ IN PROGRESS

**File/location:** `.project-plans/milestone-9/plan.md` lines 39–46 (item 4) and 121–124 (step 6);
`.project-plans/milestone-9/calibration-analysis.md` (no matching content anywhere in §§1–10).

**Problem:** The plan explicitly calls for comparing self-reported confidence against retrieval quality
(`ScoredChunk.Score`), citation coverage (`GroundingResult.AllCitationsExist`), the Source Support
classification, explicit-vs-inferred policy support (`GroundingAssessment.Citations[].SupportLevel`), and
source conflict (`GroundingAssessment.ConflictDetected`) — "for a sample of real runs... written up
narratively, not computed" (step 6). Searching `calibration-analysis.md` for any of these terms — "retrieval
quality," "citation coverage," "ConflictDetected," "SupportLevel," "AllCitationsExist" — returns zero matches.
This deliverable was not partially done and left incomplete; it was not attempted at all.

**Why it matters:** This isn't a minor omission — it's one of the six numbered items in "What We Will Build"
and one of the milestone's five "AI Concepts Being Learned" ("investigate... whether combining these signals
looks more informative than self-reported confidence alone"). The `drew-extra-card` diagnosis in §8 comes
close in spirit (it did compare confidence against the grounding outcome for one scenario), but it's a single
ad-hoc case study prompted by a striking result, not the systematic comparison across a sample of runs the
plan specified. The milestone's own "What I Should Understand by the End" list includes "why 'combining
signals looks promising' is an exploratory finding to report" — but nothing was ever combined or compared to
report on.

**Recommended direction:** With 49 real observations now in hand (up from the single-digit counts available
during initial implementation), this is newly practical to do properly: for at least the 13 incorrect
observations (and ideally a matched sample of correct ones), pull each one's `Criteria` breakdown (see finding
1 — this is also the fix that unblocks this one) and note whether low confidence correlated with poor
retrieval/citation/conflict signals, or whether (as `drew-extra-card` suggests) confidence stayed high
regardless. A few paragraphs in `calibration-analysis.md`, not a new automated scoring criterion, matching the
plan's own "written up narratively, not computed" scope.

**Progress, and a correction to the recommendation above:** the recommendation assumed the 49 existing
observations could support this comparison retroactively — checking today's raw logs found that's wrong. Only
the terse predicted-probability/outcome pair was ever printed by `calibrate`; the retrieval score, Source
Support, citation detail, and conflict flag were live in memory at the time but never captured or persisted,
so they're genuinely gone, not just unread. Two things were done in response: (1) `CalibrationObservation` now
captures `ValidatedSourceSupport`, `AllCitationsExist`, `ConflictDetected`, and citation-support-level counts
from `ScenarioTrajectory.Grounding` (same fix pattern as #1, applied to the specific fields this finding
named), and (2) `calibrate` now prints a `Grounding:` line for every incorrect observation live, so the next
session's data comes with this detail automatically. The actual narrative comparison — the deliverable itself
— is still only supported by one real case study (`drew-extra-card`, §8), honestly labeled in
`calibration-analysis.md` §11 as one data point, not the sample the plan asked for. **Still open**: writing
that comparison for a real sample requires a fresh `calibrate` run, which requires live quota this session
didn't have.

---

## ⚠️ Consider Improving

- **`calibration-analysis.md`'s header date (line 6: "Date: 2026-08-20") is stale** — the document's content
  now spans two dates (§§1–7 are 2026-08-20, §§8–10 are 2026-08-21), each internally dated at the section
  level, but the top-of-document metadata line still reads as if everything happened on one day. Low-stakes
  (each section's own prose is accurate), but worth updating to something like "2026-08-20 – 2026-08-21" so a
  reader skimming just the header isn't misled.
- **`RunCalibration`'s `runLabel` omits `scenario.Category`** (`Program.cs` line 818:
  `$"[{scenario.Id}] (run {run}/{repeatCount})"`), unlike `RunScenarioEval`'s equivalent label which includes
  it. Minor inconsistency, and ties into finding 1 above — if `Category` were actually being used for
  anything, this omission would be more noticeable.
- **`implementation-summary.md`'s top-level "What Changed" section (lines 33–64) doesn't mention the
  `TaskCanceledException` fix, `CallCountingLlmClient`, or the pacing logic** — all three are real,
  significant parts of what got built, but they only appear in later "Follow-up"/"Session 2" sections further
  down the document. A reader who only reads "What Changed" would miss that `Program.cs`'s `RunScenarioEval`
  was also modified (for the shared `TaskCanceledException` fix) and that a new `AI/CallCountingLlmClient.cs`
  file exists. Not wrong, just easy to miss — the dated-addendum structure this project uses elsewhere works
  better when the top-level summary is kept in sync, or explicitly says "see follow-up sections below for
  what changed after this list was written."
- **`implementation-summary.md`'s "Git Status" section (lines 204–214) doesn't list `AI/CallCountingLlmClient.cs`
  or `PokeJudge.Tests/AI/CallCountingLlmClientTests.cs`** among the new files — a small factual gap in an
  otherwise-accurate section, from the same "written before the follow-up work, never updated" cause as the
  point above.

---

## 🧪 Learning Observations

- **The daily-quota discovery (500 `generateContent` requests/day, confirmed live) is one of the best
  real-infrastructure findings in this whole project's evaluation-hardening arc.** It wasn't assumed or looked
  up — it was discovered by hitting it, mid-command, and reading the actual 429 payload. Worth understanding
  concretely: free-tier API constraints for a real product aren't a single number (per-minute *and* per-day,
  independently enforced, only discoverable by exhausting each).
- **The overconfidence finding strengthening rather than reversing across four independent batches (11 → 40 →
  49 observations, each batch run at a different time, under different conditions) is a genuine demonstration
  of why replication matters in evaluation.** A single batch's result could plausibly be a fluke; four
  batches pointing the same direction is a materially different kind of evidence, and the write-up correctly
  treats it as such rather than just reporting the final number.
- **The `drew-extra-card` diagnosis is the strongest worked example in this milestone of "self-reported
  confidence and Source Support are architecturally independent signals for a reason"** — the model could not
  have known its own retrieval had failed, because the confidence-estimation prompt was deliberately never
  shown that information. Worth re-reading `calibration-analysis.md` §8's diagnosis alongside the design
  decision in `plan.md` step 1 that made this diagnosis possible in the first place.
- **The gap in Must Fix #1/#2 is itself a useful lesson, not just a defect to fix**: building the
  *infrastructure* for a deliverable (the `Criteria` field, explicitly justified) is not the same as
  *delivering* it. Worth noticing the difference between "I added a field that could support this analysis"
  and "I did the analysis" — the first is easy to mistake for the second when writing up what got built.

---

## 🎯 Learning Objective Check

1. **What AI concept was this milestone intended to teach?** The difference between model confidence and
   validated correctness; what calibration means and how it's measured (reliability diagrams, ECE, Brier
   score); recognizing when a dataset is too small for a given statistic; why self-reported confidence and
   evidence-based reliability signals (Source Support) are architecturally different things.

2. **Does the implementation expose that concept clearly?** Mostly yes, and unusually concretely for an
   exploratory milestone — the sample-size-adequacy check firing correctly and honestly at every real sample
   size tried, the Brier-score-with/without-known-issues comparison, and the `drew-extra-card` diagnosis are
   all genuine, hands-on demonstrations. The gap: the milestone also wanted to teach "compare confidence
   against *other* reliability signals, not just ground truth" (retrieval quality, citation coverage, conflict
   detection), and that half of the concept was never actually demonstrated (Must Fix #2) — a developer
   finishing this milestone as currently written would not have hands-on exposure to that specific comparison,
   only to confidence-vs-outcome.

3. **What should the developer be able to explain after completing this milestone?** Why "the model said 85%"
   isn't a probability until checked against real outcomes; why Source Support and confidence had to be
   separate, independently-produced signals (and what would have gone wrong if they weren't — the
   confidence-anchoring risk that motivated excluding `SourceSupport` from the confidence prompt); how to
   recognize when a sample size can't support a statistic and what to report instead; the difference between
   per-minute and per-day API rate limits and why both matter for planning real evaluation work.

4. **Is any abstraction hiding something the developer should understand directly?** No abstraction problem —
   if anything, the opposite continues here (visible pacing waits, visible infrastructure-failure counts,
   visible per-run confidence/outcome pairs). The issue found in this review isn't hidden complexity, it's the
   inverse: a field (`Criteria`) that exists specifically to make something visible, but that visibility was
   never actually wired up to anywhere a developer would see it.

---

## 📋 Plan Completion

| Plan step | Status |
|---|---|
| 1. Add `ConfidenceEstimator`, deliberately blind to grounding/Source Support | **Complete**, design decision sharpened and locked in with a dedicated test during implementation |
| 2. Add `ConfidenceEstimateSchema`/`SystemPrompts.ConfidenceEstimation` | **Complete** |
| 3. Wire `ConfidenceEstimator` into `ScenarioEvalRunner` and the console flow | **Complete** |
| 4. Build `CalibrationAnalysis` (bucketing, ECE/Brier, `Criteria` capture) | **Complete** — `Category`/`Criteria` now also surfaced via `SummarizeByCategory`/`SummarizeCriterionFailures`, resolving Must Fix #1 |
| 5. Add `dotnet run -- calibrate` | **Complete**, later extended with automatic pacing beyond the original plan |
| 6. Manually compare confidence against other reliability signals | **Partially done** — data capture and live surfacing fixed; the narrative comparison itself still needs a fresh sample — see Must Fix #2 |
| 7. Run the real experiment | **Complete, and substantially exceeded** — 4 separate live data-gathering passes across 2 days, not a single planned run |
| 8. Tag `missed-prize`/`mulligan-not-taken`, run with/without comparison | **Complete**, and validated with real data showing the exclusion's effect concretely (Brier 0.2603 → 0.2282) |
| 9. Write the required limitations analysis | **Complete**, and unusually thorough — includes a genuinely new, unplanned investigation (§10) into the 6 zero-yield scenarios |
| 10. Make and document the end-of-milestone product decision | **Complete**, reasoning updated honestly as evidence accumulated across sessions |
| 11. Update/add tests | **Complete** — 258/258 passing, including tests added during a genuine final-coverage-review gap catch and Must Fix #1's resolution |
| *(unplanned)* Fix the `TaskCanceledException` infra-handling gap | **Not in the original plan; done as a justified, narrow, in-scope fix** once it started blocking the plan's own execution |
| *(unplanned)* Automatic pacing (`CallCountingLlmClient`) | **Not in the original plan; a reasonable, well-tested addition** requested mid-milestone to make the live experiment practically runnable |

---

## Final Verdict

**Ready After Minor Fixes**

*(Must Fix #1 resolved 2026-08-21 — `CalibrationAnalysis.SummarizeByCategory`/`SummarizeCriterionFailures`,
test-first, wired into `PrintCalibrationReport`. Must Fix #2 advanced but not closed — see below.)*

The core mechanism — the new confidence signal, its deliberate independence from Source Support, the
calibration-analysis module, and the live experiment — is well-built, genuinely tested, and validated against
real API behavior across four separate live sessions, not just unit tests. The product decision is reasoned
and evidence-gated exactly as the milestone intended. What keeps this from "Ready to Complete": one real,
specific deliverable from the approved plan — comparing confidence against the pipeline's other reliability
signals (retrieval quality, citation coverage, conflict detection) — still hasn't been written. Investigating
Must Fix #2 found the existing 49 observations genuinely can't support it (the needed data was never captured
at the time, not just unread — a correction to this review's own initial recommendation); `CalibrationObservation`
now captures it and `calibrate` now prints it live for future incorrect observations, and one real case study
(`drew-extra-card`) demonstrates what the comparison finds, honestly labeled as one data point, not a sample.
What remains is genuinely just live data-gathering plus a short narrative once that data exists — no more
infrastructure work, but not yet done.
