# Milestone 8 — Implementation Summary

**Milestone:** Milestone 8 — Evaluation
**Branch:** `milestone/8-evaluation`

## Milestone Implemented

Milestone 8 — Evaluation (PRD roadmap #8). Adds a measurement layer over Milestones 1-7's existing pipeline:
a hand-authored scenario dataset (including branching/trajectory scenarios), a harness that drives the real
`ClarificationLoop` → `RulingGenerator` → `GroundingValidator` sequence with a scripted judge, and a
deterministic scorer comparing each captured trajectory against hand-authored expected criteria. No new AI
capability was introduced — this milestone measures what already exists.

## What Changed

- **`Evaluation/EvalScenario.cs`** (new) — `ExpectedTrajectoryOutcome` enum (`SufficientOnFirstTurn` /
  `RequiresOneClarification` / `ExpectedToFailLoudly`) and the `EvalScenario` record. Branching scenarios are
  a flat representation (two rows sharing an `InitialDescription` and `BranchGroup`), not a nested tree — PRD
  §18 explicitly leaves this open, and a flat list is simpler to run (one real pipeline execution per row) and
  simpler to score.
- **`Evaluation/EvalDataset.cs`** (new) — 8 hand-authored scenarios covering 7 of PRD §15's required
  categories. Most reuse queries and expected sections already verified in Milestones 5-7's real runs
  (`RetrievalEvalSet`, `observed-limitations.md`); one new scenario (`drew-extra-card`) was verified live via
  `dotnet run -- search` before being added, matching this project's evidence-based practice rather than
  guessing at expected sections.
- **`Evaluation/ScenarioTrajectory.cs`** (new) — `TurnRecord` and `ScenarioTrajectory`, with named static
  factories (`Failed`/`TurnCapExhausted`/`Completed`) mirroring the existing `ClarificationOutcome` pattern.
- **`Evaluation/ScenarioEvalRunner.cs`** (new) — drives the real `ClarificationLoop` with a scripted
  `askJudge` (returns the scenario's pre-authored answer once, then a real, informative "asked more than
  scripted" signal instead of crashing). Catches the specific `InvalidOperationException` Milestone 2's
  "insufficient with zero questions" guard throws and returns it as a scored outcome for scenarios that
  expect it, rather than letting it propagate. Reuses the `Program.cs`-established "capture last turn's
  chunks from `onAssessment`" pattern instead of a second, redundant retrieval call.
- **`Evaluation/ScenarioEvalScorer.cs`** (new) — pure, deterministic scoring: initial retrieval (reusing
  Milestone 5's `RetrievalEvaluator.Evaluate` unchanged), sufficiency timing, clarifying-question materiality
  (via `RelatedChunkId`'s section), post-answer retrieval, and final Source Support acceptability (reusing
  Milestone 7's `GroundingResult` unchanged). Only criteria applicable to a scenario's expected outcome are
  scored — an omitted criterion is not the same as a passed one.
- **`Evaluation/EvalScenarioSelector.cs`** (new, added after the real harness run below surfaced a rate-limit
  problem) — pure arg-parsing for `--from <scenario-id>` / `--only <scenario-id>`, letting a developer resume
  past the free-tier chat-completion rate limit or re-check a single scenario without re-running the whole
  dataset. Scoped entirely to this developer/CI-facing tool; the judge-facing product only ever makes one
  request at a time, so no equivalent retry/backoff was added to the shared `GeminiLlmClient`.
- **`Program.cs`**: new `dotnet run -- evaluate [--from <id>] [--only <id>]` command, separate from Milestone
  5's `eval` (retrieval-only, unchanged). Prints a per-scenario, per-criterion pass/fail breakdown plus both
  Source Support labels, and a final summary count.

No new NuGet packages. `Evaluation/` is a new top-level module, matching PRD §11's illustrative structure,
which names it as a peer of `AI`/`State`/`Retrieval`/`Ingestion` from roughly Milestones 5-8 onward.

## Validation

- **Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
- **Tests:** `dotnet test` — 187/187 passed (154 carried over from Milestone 7, 33 new).
  - **Written first (Red step), all deterministic:** `ScenarioEvalScorerTests` (18 cases, covering every
    criterion and outcome combination, table-driven in spirit), `ScenarioEvalRunnerTests` (5 cases, stub-based,
    mirroring `ClarificationLoopTests`/`GroundingValidatorTests`'s established pattern — verifying
    orchestration and trajectory capture, not live model quality), `EvalScenarioSelectorTests` (9 cases,
    added after the real run below surfaced the rate-limit problem `--from`/`--only` solves).
  - **Final coverage-review pass:** added a test for `ScenarioEvalReport.AllPassed`'s empty-criteria guard
    (real logic, never exercised through `Score()`'s current paths, but worth locking in directly — same
    principle as Milestone 7's `AllCitationsExist` treating an empty list as a fail, not a vacuous pass).
  - **A second gap found by the real run, not by unit testing** (see below): `Score()` didn't distinguish an
    *expected* failure from an *unexpected* one for non-`ExpectedToFailLoudly` scenarios, producing a
    misleading report when `deck-not-shuffled` crashed for real. Fixed with an explicit "Unexpected failure"
    criterion checked before outcome-specific scoring, and a regression test added directly from the
    observation (`Score_NotExpectedToFailButDidAnyway_...`).
- **Real-data validation** (see `observed-limitations.md` for full detail): three full attempts at
  `dotnet run -- evaluate` against the real, expanded corpus (515 chunks). None completed all 8 scenarios in
  one run — the free-tier chat-completion rate limit (15 req/min) truncated every attempt, and the harness had
  no equivalent of the chunk-embedding pipeline's resumability, so each retry re-ran already-passing
  scenarios. After adding `--from`/`--only` specifically to address this, `dotnet run -- evaluate --only
  drew-extra-card` reached the one scenario none of the three full runs did, on the first attempt — direct
  confirmation the fix works, and a third real, independent reproduction of the missed-Prize/deck-not-shuffled
  malformed-response crash (see `observed-limitations.md` §6).

## Intentional Limitations

- **A small, hand-authored dataset with no statistical claim attached.** 8 scenarios catch gross regressions;
  they say nothing about the system's general error rate. This is the exact limitation Milestone 9 is
  required to grapple with explicitly.
- **No LLM-as-judge scoring of ruling prose quality.** Deliberately not built — Milestone 7 already
  demonstrated why an LLM checking LLM output isn't independent verification; repeating that pattern here
  would ignore that finding rather than apply it.
- **Clarifying-question materiality is a structural proxy** (does `RelatedChunkId`'s section match an
  expected one), not a semantic judgment of whether the question's phrasing was actually good judge-facing
  language.
- **No persisted, historical report system.** `observed-limitations.md` fills PRD §15's "simple run log" role
  for this run, matching every prior milestone's practice, rather than building report-history
  infrastructure.
- **Branching scenarios are limited to a single branch point** (flat rows sharing a `BranchGroup`), not a
  general recursive tree — matches PRD §15's own worked example; no concrete need for deeper nesting has
  appeared.
- ~~**The harness has no resumability across a rate-limited run**~~ **Addressed, deliberately minimally.**
  `--from`/`--only` let a developer manually resume past a rate limit or re-check one scenario, without full
  automatic checkpointing (unlike Milestone 4's chunk/embed pipeline, which persists progress to disk after
  every batch). This is a considered scope decision, not an oversight: `evaluate` is developer/CI-facing
  tooling the judge-facing product never touches, so a manual flag is proportionate — automatic
  retry/backoff would only exist to serve batch-testing convenience, not any real product need. Directly
  confirmed working: `dotnet run -- evaluate --only drew-extra-card` reached the one scenario three full runs
  never did (see `observed-limitations.md` §6). Full automatic checkpointing remains a real option if this
  dataset grows large enough that manual resumption becomes tedious — not needed at 8 scenarios.

## Learning Focus

- **Trajectory (process) evaluation, demonstrated by a run the plan didn't script.** The `special-condition`
  scenario resolved with zero clarifying questions on one live run — a materially different investigation
  path than every prior manual observation — and while its final Source Support still passed, three
  trajectory-level criteria correctly failed. A destination-only eval would have called this run a clean
  success; PRD §15's actual argument for trajectory scoring was observed directly, not just implemented.
- **Deterministic scoring over non-deterministic behavior, continued from Milestone 7.** `ScenarioEvalScorer`
  is the same kind of piece `SourceSupportAssigner` was: fully pure, thoroughly tested, and completely unable
  to make the live pipeline's own behavior deterministic — it can only judge it fairly once captured.
- **Reuse over reinvention.** This milestone's harness introduces no new retrieval, grounding, or generation
  logic — it wires together `RetrievalEvaluator` (Milestone 5), `ClarificationLoop`/`RulingGenerator`
  (Milestone 6), and `GroundingValidator` (Milestone 7) exactly as they already exist.
- **Real non-determinism as a first-class finding, not noise to average away.** The same scenario producing
  three different trajectories across three runs is itself evidence worth carrying into Milestone 9, not a
  nuisance to explain away before it can be used.

## What I Should Try

1. Re-read `observed-limitations.md` §3 (the `special-condition` divergence) and decide for yourself: does a
   `ScenarioEvalReport` where the final-answer criterion passes but the trajectory criteria fail deserve to
   count as "the scenario passed"? `ScenarioEvalReport.AllPassed` currently says no (any single failing
   criterion fails the whole report) — do you agree that's the right call?
2. Run `dotnet run -- evaluate` yourself, ideally spaced out to actually reach all 8 scenarios, and see
   whether `deck-not-shuffled` and `spectator-badges` behave the same way you observe here, or differently
   again — direct, personal evidence of the run-to-run variability documented in `observed-limitations.md`.
3. Think about what a minimal resumability mechanism for `RunScenarioEval` would need (see Intentional
   Limitations) — is it worth building, or is spacing out manual runs sufficient at this dataset's current
   size?
4. Try adding one more hand-authored scenario for a category this dataset doesn't cover yet (e.g., "incorrect
   attack resolution" or "timing questions") — a good exercise in the same evidence-gathering discipline used
   to build this dataset (verify expected sections via `dotnet run -- search` first, don't guess).

## Git Status

- **Branch:** `milestone/8-evaluation`
- **Uncommitted:** yes — all implementation changes are in the working tree, nothing staged or committed yet
  (this skill does not commit automatically).
- **Unexpected files:** none. `git status` shows exactly the expected changes: modified `Program.cs`; new
  `Evaluation/` (production) and `PokeJudge.Tests/Evaluation/` (tests) directories; new
  `.project-plans/milestone-8/` documents (`plan.md`, `observed-limitations.md`, this file).
