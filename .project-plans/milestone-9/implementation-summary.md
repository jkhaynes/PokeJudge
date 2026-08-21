# Milestone 9 — Implementation Summary

**Milestone:** Milestone 9 — Confidence Calibration and Reliability
**Branch:** `milestone/9-confidence-calibration`

## Milestone Implemented

Milestone 9 — Confidence Calibration and Reliability. Adds a new, self-reported confidence signal
(`ConfidenceEstimator`) deliberately kept independent of the existing Source Support signal, a deterministic
calibration-analysis module (`CalibrationAnalysis`) comparing that signal against real scored outcomes from
the Milestone 8.5 hardened dataset, and a new `dotnet run -- calibrate` command that runs the pipeline and
reports Brier score, coarse bucket comparisons, and Expected Calibration Error only when the sample size
actually supports it. Covers plan.md's full step list (1–11): the mechanism, its unit tests, the live
experiment, the required limitations analysis, and the end-of-milestone product decision — see
`.project-plans/milestone-9/calibration-analysis.md` for the full write-up.

**Headline result, updated across three data-gathering passes on 2026-08-21: 49 total real observations**
(11 from session 1's un-paced experiment, 29 from session 2's automated paced `calibrate` run, 9 more from a
third pass that ran until the *daily* quota was hit — confirmed for the first time to be exactly 500
`generateContent` requests/day, shared across every command run that day). Each successive batch reinforced
the same direction, not reversed it: a real, ~4-standard-deviation overconfidence gap in the model's dominant
95-100% confidence range (mean predicted ~98.5% vs. observed correct ~73-77%), with at least one concretely
diagnosed case (`drew-extra-card`, investigated via `evaluate`, not just observed) showing high self-reported
confidence alongside a validated-Insufficient ruling the confidence step had no way to know about. Per PRD
SS9's requirement that a numeric confidence signal be "empirically validated as calibrated" before judges ever
see it, **the product decision is: Source Support remains the sole judge-facing signal; confidence work stays
internal to evaluation** — now for a stronger reason than session 1's "not enough evidence": the evidence
gathered, though still short of a full calibration curve, consistently points toward real overconfidence.
Full reasoning in `calibration-analysis.md` §§4-9.

## What Changed

- **`Reliability/ConfidenceEstimate.cs`** (new) — `ConfidenceEstimate(PredictedCorrectnessProbability: int,
  Rationale: string)` record and hand-written schema, mirroring `RulingResultSchema`'s pattern. First use of
  an `INTEGER` schema type in this project — confirmed live against the real Gemini API, not assumed to work.
- **`Reliability/ConfidenceEstimator.cs`** (new) — a separate LLM call, mirroring `GroundingValidator`'s shape,
  producing the confidence estimate from the already-generated ruling. **Deliberately excludes the grounding
  result and the ruling's own self-assigned `SourceSupport`/`SourceSupportRationale`** from its input — a
  design point confirmed explicitly during implementation (not just at planning time): showing either back to
  the model risks the confidence estimate anchoring to a pre-existing label instead of independently
  re-examining the material, which would make the milestone's central confidence-vs-Source-Support comparison
  circular rather than informative.
- **`Reliability/CalibrationAnalysis.cs`** (new) — pure, deterministic functions over captured
  `CalibrationObservation`s: `Bucket` (groups into N ranges, computing mean predicted probability and observed
  correct rate per bucket), `BrierScore`, `ExpectedCalibrationError`, `BucketsSupportFineGrainedEce` (the
  sample-size-adequacy check, made testable rather than left as prose), and `ExcludeKnownIssues` (filters out
  `missed-prize`/`mulligan-not-taken`, per plan.md's addendum).
- **`Clarification/SystemPrompts.cs`** — added `ConfidenceEstimation`, explicit that this is a self-assessment
  with no separate validation result to reference.
- **`Clarification/PromptBuilder.cs`** — added `BuildConfidenceEstimationPrompt`, which includes the scenario,
  confirmed facts, retrieved passages, and the ruling's substance (recommendation, explanation, repair steps,
  penalty guidance, cited chunk IDs) but never its Source Support label.
- **`Evaluation/ScenarioTrajectory.cs`** — added an optional `Confidence` field (defaults to `null`), populated
  only on the `Completed` path.
- **`Evaluation/ScenarioEvalRunner.cs`** — now also takes a `ConfidenceEstimator` and calls it after grounding
  validation on the `Completed` path. `ScenarioEvalScorer`'s criteria are untouched, per the plan's explicit
  scope boundary — this milestone consumes `AllPassed` as ground truth, it doesn't redefine it.
- **`Program.cs`** — wired `ConfidenceEstimator` into both the interactive console flow (printed clearly
  labeled "internal, unvalidated, not for judge display," per PRD SS9) and `RunScenarioEval`. Added
  `dotnet run -- calibrate [--from/--only/--repeat]`, reusing `EvalScenarioSelector` and the same
  `ScenarioEvalRunner` pipeline `evaluate` uses; a run that never produces a ruling/confidence is correctly
  excluded from the calibration set, not treated as an error.

No new NuGet packages. No changes to `ScenarioEvalScorer`'s scoring criteria or `ClarificationLoop`.

## Validation

- **Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
- **Tests:** `dotnet test` — 251/251 passed (219 carried over from Milestone 8.5, 28 from the initial
  implementation pass, 4 more from the follow-up pacing work).
  - **Written first (Red step):** `CalibrationAnalysisTests` (23 cases covering `Bucket`'s boundary math
    including exactly-0/exactly-100/out-of-range clamping, `BrierScore`'s perfect/worst/mixed cases,
    `ExpectedCalibrationError`'s perfect-calibration/miscalibrated cases, `BucketsSupportFineGrainedEce`'s
    threshold logic, and `ExcludeKnownIssues`'s filtering).
  - **Added during the final coverage-review pass:** 5 new `PromptBuilderTests` cases for
    `BuildConfidenceEstimationPrompt` — this project has an established convention of unit-testing every
    `PromptBuilder.Build*Prompt` method that the initial Red step missed. One of these five
    (`NeverIncludesTheRulingsOwnSourceSupportLabelOrRationale`) directly locks in the "deliberately blind"
    design decision confirmed during implementation, rather than leaving it as an unverified comment.
  - `ScenarioEvalRunnerTests`'s existing `Completed`-path tests were extended with `ConfidenceEstimate`
    enqueues and `trajectory.Confidence` assertions rather than left unchanged (their behavior did change:
    every completed run now also produces a confidence estimate).
- **Live smoke test:** `dotnet run -- calibrate --only notes` against the real corpus and live model —
  confirmed the new `INTEGER` schema type deserializes correctly (untested against the real API before this),
  the full pipeline round-trips end to end, `Brier score`/bucket table print correctly, and the
  insufficient-sample-size fallback message fires exactly as designed (1 observation is nowhere near the
  30-per-bucket threshold).

## Live Experiment Findings (steps 7–9, now complete)

Full detail in `calibration-analysis.md`; summarized here:

- **One un-paced `calibrate --repeat 5` across the full dataset yielded 4/100 usable observations** (96
  infrastructure failures) — the per-minute quota exhausted within the first two scenarios and never recovered
  within the same fast-running command.
- **Switching to Milestone 8.5's proven paced, per-scenario approach** (`--only <id> --repeat 2`, ~75s waits
  between commands) reliably produced real data, but at a much smaller per-command scale than plan.md assumed
  — **11 total real observations** across 5 scenarios by the time this pass wrapped up.
- **A second, new infrastructure-handling gap was found live**: raw `TaskCanceledException` connection
  timeouts (not 429 responses) crashed the whole command twice, since the existing infra-failure handling
  (inherited unchanged from Milestone 8.5) only catches `HttpRequestException`. Initially documented as a real
  finding, left unfixed as out of scope. **Reversed the next session (2026-08-21):** it recurred a third time,
  immediately, at the very start of the automated-pacing run — now directly blocking the plan it was supposed
  to enable, not just a documented risk. Fixed in both `RunCalibration` and `RunScenarioEval` (`Program.cs`),
  extending the existing `HttpRequestException` catch to also catch `TaskCanceledException`, exactly the same
  treatment the established pattern already gives a 429. See `calibration-analysis.md` §8.
- **A genuine bug was found and fixed during this pass**: `RunCalibration`'s "excluding known-issue scenarios"
  report crashed with an unhandled `ArgumentException` when every captured observation happened to be a
  known-issue scenario (so the filtered list was empty) — `CalibrationAnalysis.BrierScore` correctly throws on
  zero observations, but the call site didn't guard against it. Fixed in `Program.cs` with an explicit
  zero-count check; this is orchestration glue code, consistent with this project's established practice of
  not unit-testing `Program.cs` directly, so no new test was added for the fix itself (the underlying
  `BrierScore` empty-input behavior was already covered by `BrierScore_EmptyObservations_Throws`).
- **The dataset was too small to support any of plan.md's targeted statistics** — not even the "coarse" 2-3
  bucket comparison (all 11 observations landed in a single bucket). Brier score (0.0825 overall; 0.0003
  excluding the two `mulligan-not-taken` observations) is the only number reported with any real meaning at
  this sample size, and even that comes with an explicit small-sample caveat in the write-up.
- **Product decision: Source Support remains the sole judge-facing signal.** Confidence work stays internal to
  evaluation — the honest consequence of insufficient evidence, not a finding that confidence is miscalibrated.

## Follow-up: Automatic Pacing for the Next Data-Gathering Session

After the live experiment, added self-pacing to `RunCalibration` so a future session doesn't need ~14
manually-timed commands to gather more data: a new `CallCountingLlmClient` decorator
(`PokeJudge/AI/CallCountingLlmClient.cs`, unit-tested) tracks real `generateContent` calls made through it;
`RunCalibration` checks the running count against the free-tier's 15-per-minute budget before each run and
waits out the remainder of a 62-second window when a run's estimated worst-case cost would exceed it, printing
the wait so it's visible rather than silent. Scoped to `calibrate` only — `evaluate` and the interactive
console flow are untouched. Full detail and the resulting one-command execution plan are in
`calibration-analysis.md` §7 (updated). Not live-tested against the real API (today's quota was already
exhausted by the time this was built); validated via `CallCountingLlmClientTests` (4 new tests) and code
review — its correctness will be confirmed for real the next time `calibrate` actually runs.

## Session 2 (2026-08-21): the pacing plan executed, live-validated, and extended

The automated pacing worked as designed the first time it ran for real: `dotnet run -- calibrate --from
deck-not-shuffled --repeat 3` completed unattended across 18 scenarios in one command, pacing waits fired 27
times exactly as intended, and the newly-fixed `TaskCanceledException` handling caught one recurrence
gracefully instead of crashing. **29 new observations, 40 total combined with session 1.** Full results,
including the `drew-extra-card` finding (investigated via `evaluate`, not just observed — a genuine case of
high self-reported confidence alongside a validated-Insufficient ruling) and the updated, strengthened product
decision, are in `calibration-analysis.md` §8.

## Follow-up: Review Fix — Category/Criteria Surfaced (2026-08-21)

The milestone review (`review.md`) flagged that `CalibrationObservation.Category` and `.Criteria` were
populated on every observation but never read anywhere — the exact "captured but never consumed" pattern this
project has already flagged twice before (Milestone 8's `BranchGroup`, Milestone 8.5's
`SourceCoverageFindings` header comment). Fixed following the review's option (b): added
`CalibrationAnalysis.SummarizeByCategory` and `.SummarizeCriterionFailures`, mirroring
`Evaluation/CategorySummary.cs`'s established grouping pattern exactly (first-seen order, not alphabetized).
Both are pure and test-first (7 new tests). Wired into `Program.cs`'s `PrintCalibrationReport`, so every
`calibrate` run now prints a per-category breakdown and a criterion-failure breakdown among incorrect
observations, alongside the existing Brier score and bucket table. Full test suite: 258/258. The milestone's
second review finding — the plan's "compare confidence against other reliability signals" deliverable was
never executed — remains open; this fix supplies the data that comparison would need, but doesn't write the
comparison itself.

## Follow-up: Review Fix — Grounding Signals Captured, Narrative Comparison Still Pending (2026-08-21)

Investigating Must Fix #2 (the never-executed "compare confidence against other reliability signals"
deliverable) found the 49 already-gathered observations can't support it retroactively — checking today's raw
`calibrate` logs confirmed only the terse predicted-probability/outcome pair was ever printed; the retrieval
score, Source Support, citation detail, and conflict flag were live in each run's `GroundingResult` at the
time but never captured or persisted, so they're genuinely gone, not just unread. `CalibrationObservation`
now captures `ValidatedSourceSupport`, `AllCitationsExist`, `ConflictDetected`, and citation-support-level
counts from `ScenarioTrajectory.Grounding`, and `calibrate` now prints a `Grounding:` line for every incorrect
observation live, so the next data-gathering session's incorrect observations come with this detail
automatically. The actual narrative comparison itself is still only supported by one real case study
(`drew-extra-card`, already documented) — honestly labeled in `calibration-analysis.md` §11 as one data point,
not the sample the plan asked for. Writing that comparison for a real sample is the concrete next step, and
requires live quota this session didn't have. Full test suite: 258/258.

## Intentional Limitations

- **The confidence signal remains directionally, not conclusively, characterized.** 40 observations (up from
  11) show a real, multi-standard-deviation overconfidence gap in the model's dominant 95-100% range, but 39
  of those 40 land in that single bucket — a full calibration curve across the whole 0-100% range remains out
  of reach at this dataset's size. See `calibration-analysis.md` §4/§6/§8. Getting a fuller picture would need
  either many more sessions like this one, or the free-tier constraint resolved some other way.
- **Per-bucket sample sizes were too small for a fine-grained ECE — confirmed, not just predicted.**
  `BucketsSupportFineGrainedEce`'s 30-per-bucket threshold correctly returned `false` against the real 11-
  observation dataset; the tool reported that honestly rather than a precise-looking ECE number the data
  couldn't support. Even the "coarse" 2-3 bucket comparison plan.md scoped as the realistic fallback wasn't
  meaningfully supported either — all 11 observations landed in a single bucket.
- **`ActualCorrect` still folds several different criteria into one boolean**, even though each
  `CalibrationObservation` now also carries the full `Criteria` breakdown for manual, narrative
  interpretation — no automated attribution logic decomposes *which* criterion drove a miscalibration, per
  the plan's explicit scope (step 6: "written up narratively, not computed").
- **Confidence estimation adds one more LLM call to every completed `evaluate` run**, not just `calibrate`
  runs, since both share `ScenarioEvalRunner`. This increases real API cost/rate-limit pressure on the
  already-documented free-tier constraint (Milestone 8.5's finding 8) — an accepted tradeoff of the approved
  design (maximizing pipeline reuse over maintaining two near-duplicate runners), not an oversight.

## Learning Focus

- **A raw model-reported number is not a probability until checked against real outcomes — and "checked"
  can honestly come back inconclusive.** The confidence estimate the model produces is just more model output
  text; running the real experiment didn't produce a clean "calibrated" or "miscalibrated" verdict, it
  produced "not enough evidence yet either way" — an outcome worth recognizing as a legitimate, common result
  of an evaluation, not a failed one.
- **Recognizing when a dataset is too small for a statistic, demonstrated with real numbers, not just
  predicted at planning time.** `mulligan-not-taken`'s Brier-score contribution disappearing almost entirely
  once excluded as a known issue (0.0825 → 0.0003) is a concrete, worked example of exactly the
  "don't misattribute a known bug to new evidence" principle plan.md's addendum described in the abstract.
- **Why Source Support and self-reported confidence had to be built as genuinely independent signals**,
  demonstrated concretely by the mid-implementation design decision to exclude both the grounding result *and*
  the ruling's own prior `SourceSupport` label from the confidence prompt — a subtler version of the same
  principle than what was originally approved in planning, only visible once the actual prompt content was
  being written.
- **Recognizing a testing-convention gap during the coverage-review pass, not just writing tests for new
  logic.** The initial Red step covered `CalibrationAnalysis` but missed that this codebase already has an
  established pattern (every `PromptBuilder.Build*Prompt` method gets tested) — the final coverage-review pass
  exists specifically to catch this kind of gap against the real diff, not just against the original plan.

## What I Should Try

1. Read `calibration-analysis.md`'s §3 table and §4 Brier-score breakdown, then decide for yourself: does the
   with/without-`mulligan-not-taken` comparison (0.0825 → 0.0003) convince you the one miscalibration case is
   really just that scenario's already-known bug, or would you want more independent evidence before accepting
   that? There's no single right answer — it's a judgment call about how much a single data point should move
   a conclusion, worth forming your own view on.
2. Run `dotnet run -- calibrate --only <some scenario not yet tried>` yourself, paced with a wait beforehand,
   and add your own real observation to the dataset by hand — does it land where you'd predict given the
   existing 11 (heavily clustered at 95-100%, mostly correct)?
3. Compare a few real runs' `Model's own assessment (unvalidated)` Source Support line against the new
   `Self-reported confidence` line in the interactive console flow (`dotnet run --` with no arguments) — do
   they move together, or diverge? With only 11 data points this couldn't be studied systematically; a handful
   of manual side-by-side comparisons is the closest available substitute right now.
4. If you ever do pursue gathering enough data for a real calibration verdict (plan.md's ~70-90 target), decide
   first: is that worth a paid API tier, or a deliberate multi-day pacing effort? `calibration-analysis.md`'s
   closing paragraph names both options without recommending either — worth having an opinion before deciding.

## Git Status

- **Branch:** `milestone/9-confidence-calibration`
- **Uncommitted:** yes — all implementation changes are in the working tree, nothing staged or committed yet
  (this skill does not commit automatically).
- **Unexpected files:** none. `git status` shows exactly the expected changes: modified `Program.cs`
  (includes the `RunCalibration` empty-list bug fix found during the live experiment),
  `Clarification/SystemPrompts.cs`, `Clarification/PromptBuilder.cs`, `Evaluation/ScenarioTrajectory.cs`,
  `Evaluation/ScenarioEvalRunner.cs`, and two test files (`ScenarioEvalRunnerTests.cs`,
  `PromptBuilderTests.cs`); new `Reliability/` (3 files) and `PokeJudge.Tests/Reliability/` (1 file); new
  `.project-plans/milestone-9/` documents (`plan.md`, this file, and `calibration-analysis.md`).
