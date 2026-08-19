# Milestone 8 — PR Review

### PR Review Summary

- **Milestone reviewed:** Milestone 8 — Evaluation
- **Current branch:** `milestone/8-evaluation`
- **Base branch:** `master` (this repo's default branch; the repo has no `main`)
- **What changed:** A new `Evaluation/` module (`EvalScenario`, `EvalDataset`, `ScenarioTrajectory`,
  `ScenarioEvalRunner`, `ScenarioEvalScorer`, `EvalScenarioSelector`), a new `PokeJudge.Tests/Evaluation/`
  test suite, a new `dotnet run -- evaluate [--from <id>] [--only <id>]` console command wired in
  `Program.cs`, and `.project-plans/milestone-8/` documentation (`plan.md`, `observed-limitations.md`,
  `implementation-summary.md`, `review.md`, `learning-checkpoint.md`). This review also covers the three
  Consider-Improving fixes applied after the internal milestone review (`Answer budget` scoring,
  `EvalScenario.BranchGroup` removal, `ScenarioTrajectory.FinalRetrievedChunks` removal), a new
  `Clarification/InsufficientWithoutQuestionsException.cs` added after this PR review found a precision gap
  (see Major Issues), and a separate, already-committed `docs/PRD.md` change adding Milestone 8.5 to the
  roadmap.
- **Overall impression:** Solid. The harness genuinely reuses Milestones 5-7's components unchanged, the
  scorer is a clean, pure, thoroughly-tested combinator over captured trajectories, and the milestone was
  validated against the real corpus repeatedly, with findings honestly documented rather than smoothed over.
  The one real precision gap found in how "expected failure" is classified has since been fixed and
  regression-tested (see Major Issues); everything else is minor polish.
- **Build/test status:** `dotnet build` — succeeded, 0 warnings, 0 errors. `dotnet test` — 191/191 passed at
  time of review; 192/192 after the fix below (1 new regression test).

**Update:** The Major issue below has since been fixed (direction (a)) — see its note.

**Operational note before this can become a real PR:** as of this review, only the `docs/PRD.md` Milestone
8.5 addition is actually committed on this branch (`f49b875`). The entire Milestone 8 implementation —
`Program.cs`'s changes, all of `Evaluation/`, all of `PokeJudge.Tests/Evaluation/`, and
`.project-plans/milestone-8/`'s docs — is still sitting uncommitted in the working tree. This review treats
that working-tree state as "the branch's changes" since that's what will become the PR, but nothing below
can actually be opened as a PR until it's committed. See Scope Check.

### 🚫 Blockers

None.

### ⚠️ Major Issues

#### 1. `ScenarioEvalRunner`'s `catch (InvalidOperationException)` conflated two distinct failure modes — ✅ FIXED

- ~~**`ScenarioEvalRunner`'s `catch (InvalidOperationException)` cannot actually distinguish the specific
  "insufficient with zero questions" guard it's documented as targeting from an unrelated malformed
  structured-output failure, because both throw the identical exception type.**
  (`Evaluation/ScenarioEvalRunner.cs:34-65`, `AI/StructuredResponseParser.cs:26`,
  `Clarification/ClarificationLoop.cs:62`)

  `ClarificationLoop.cs:62` throws `InvalidOperationException("Model reported the scenario insufficient but
  supplied no clarifying questions.")` — this is the specific, known bug the plan, implementation summary,
  and `observed-limitations.md` all describe the runner as catching ("the exact type Milestone 2's guard
  throws"). But `StructuredResponseParser.cs:26` throws the *same* `InvalidOperationException` type
  ("Model response deserialized to a null {T}") for *any* structured call that comes back null — including
  the sufficiency assessment and fact-extraction calls the loop makes internally. `ScenarioEvalRunner`'s
  `catch (InvalidOperationException ex)` wraps the entire `_loop.RunAsync(...)` call and treats both causes
  identically: `ThrewExpectedFailure: true`, differing only in the free-text `FailureMessage` a human might
  read but the scorer never inspects.

  **Concretely:** if `missed-prize` (`ExpectedToFailLoudly`) ever fails because of a transient malformed/null
  structured response during fact extraction — unrelated to the specific zero-questions bug it's meant to
  regression-test — `ScenarioEvalScorer.ScoreExpectedFailure` still reports "Expected failure: PASS." The
  eval report would look like confirmation of the known bug reproducing again, when it actually reproduced a
  *different* problem. This directly undercuts `observed-limitations.md` §6's claim that three scenario
  categories "independently reproduce the same failure mode first documented in Milestone 6" — that claim is
  true today only because a human read the `FailureMessage` text by eye each time, not because the code
  itself verifies it.

  **Recommended direction:** either (a) have `ClarificationLoop`'s zero-questions guard throw a distinct
  exception type (or a small sentinel/marker) instead of reusing the generic null-deserialization exception,
  so the two failure modes are structurally distinguishable, or (b) have the scorer/runner check
  `FailureMessage` content for the guard's specific text before crediting "Expected failure," or (c) at
  minimum, soften the docs' "the exact type" and "the same failure mode" language to acknowledge this is
  currently verified by manual message inspection, not automatically. (a) is the more durable fix and matches
  this project's existing pattern of specific, named exceptions for specific, known failure modes.~~ **Fixed
  (direction (a)).** Added a new `Clarification/InsufficientWithoutQuestionsException.cs`, a small `Exception`
  subclass (not an `InvalidOperationException` subclass, deliberately — so it can't be re-conflated with
  `StructuredResponseParser`'s unrelated null-deserialization guard by anyone catching that base type either).
  `ClarificationLoop.cs:62` now throws this specific type instead of `InvalidOperationException`, and
  `ScenarioEvalRunner.cs`'s catch clause now catches only `InsufficientWithoutQuestionsException`. A new
  regression test (`ScenarioEvalRunnerTests.RunAsync_UnrelatedInvalidOperationException_...`) confirms a
  generic `InvalidOperationException` from elsewhere in the loop (simulated via `StubLlmClient`'s own
  "no more queued results" guard) now propagates out of `RunAsync` instead of being silently scored as an
  expected failure — the exact failure mode this finding described, now directly locked in by a test.
  `ClarificationLoopTests`'s existing guard test was updated to assert the new exception type. Full suite
  re-run: 192/192 (1 new test), build clean.

### 🔎 Minor Issues

#### 1. Stale "branching" language in `EvalDataset.cs`'s comments — ✅ FIXED

- ~~**Stale "branching" language in `EvalDataset.cs`'s comments for `special-condition` and `drew-extra-card`**
  (`Evaluation/EvalDataset.cs:76`, `:111`) now that `EvalScenario.BranchGroup` has been removed (per the
  milestone review's Consider-Improving fix). Neither scenario ever had a sibling row sharing an
  `InitialDescription`/branch point — the review that recommended removing `BranchGroup` noted this directly
  ("no sibling row sharing it"). Calling them "branching" in the surrounding comments was already a minor
  overstatement before the removal; now that the one field that could have supported an actual branch pair is
  gone, the word is more likely to mislead a future reader into expecting branching infrastructure that
  doesn't exist. Worth a follow-up comment tweak (e.g., "discretion-required" / "gameplay error" without
  "branching") whenever this file is next touched.~~ **Fixed.** Both comments (`special-condition`,
  `drew-extra-card`) no longer say "branching" — now read "Discretion-required / illegal game state." and
  "Drawing too many cards / gameplay error." respectively, describing only what's actually true of these
  single, non-branching scenarios. Comment-only change; build clean, 192/192 tests unaffected.

### 💬 Review Notes

- The `docs/PRD.md` Milestone 8.5 commit (`f49b875`) is already merged onto this branch. It's directly
  motivated by this milestone's findings, but it documents *future* work (a not-yet-implemented Milestone
  8.5, plus edits to Milestone 9's description) rather than a Milestone 8 deliverable itself. Worth deciding
  deliberately whether it belongs in the Milestone 8 PR or should ride along in its own PR — not a defect
  either way, just a scope call worth making on purpose rather than by default.
- `ScenarioEvalScorer.ScoreSufficiencyTiming`'s `switch` has a `default` arm for an "unhandled expected
  outcome" that's unreachable in practice (the only three enum values are all handled, and
  `ExpectedToFailLoudly` never reaches this method). Harmless defensive code — not worth changing, just
  noting it's dead in current usage.

### 🤖 AI-Specific Review

The harness is a clean example of the "capture non-deterministic behavior as data, then score the data
deterministically" pattern already established by `SourceSupportAssigner` in Milestone 7 — confirmed by
reading `ScenarioEvalScorer` end-to-end: nothing in it makes an LLM call, and every criterion is a pure
function of an already-captured `ScenarioTrajectory`. Reuse of `RetrievalEvaluator.Evaluate` (Milestone 5),
`ClarificationLoop`/`RulingGenerator` (Milestone 6), and `GroundingValidator` (Milestone 7) is genuine and
unchanged — confirmed no diff exists in `Clarification/`, `Grounding/`, `Retrieval/`, or `StructuredState/`
relative to `master`.

One real instance of "could the implementation appear to work while actually relying on behavior we did not
intend?" was found and fixed (see the Major Issue note above): the harness's "Expected failure" criterion had
relied on an unverified assumption — that the only thing that could throw `InvalidOperationException` out of
the loop, for a scenario authored to expect it, was the specific known bug — that the code didn't actually
enforce. It now does, via a dedicated exception type.

No LLM-as-judge scoring was added, consistent with the plan's explicit rejection of that approach (citing
Milestone 7's finding that self-validation isn't independent). No numeric/calibrated confidence was
introduced — correctly deferred to Milestone 9.

### 🧪 Test Review

- **Coverage:** `ScenarioEvalScorerTests` is table-driven and covers every criterion for every applicable
  `ExpectedTrajectoryOutcome`, including the criterion-omission cases (e.g., "Answer budget" absent for
  `SufficientOnFirstTurn`/`ExpectedToFailLoudly`) and the `AllPassed` empty-criteria guard.
  `ScenarioEvalRunnerTests` is stub-based and correctly verifies orchestration (scripted-answer consumption,
  the "asked more than scripted" signal, turn-cap exhaustion without calling the ruling generator, the
  guarded-exception-to-trajectory conversion) rather than live model quality.
  `EvalScenarioSelectorTests` covers `--from`/`--only`/mutual-exclusion/unknown-id/malformed-flag cases
  thoroughly.
- **Missing coverage, now closed:** the Major Issue's fix added
  `RunAsync_UnrelatedInvalidOperationException_PropagatesRatherThanBeingScoredAsAnExpectedFailure`, which
  simulates a generic `InvalidOperationException` from elsewhere in the loop (via `StubLlmClient`'s "no more
  queued results" guard) and confirms it now propagates out of `ScenarioEvalRunner.RunAsync` rather than
  being caught and mis-scored as the specific known bug.
- **Build/test results:** `dotnet build` succeeded (0 warnings, 0 errors); `dotnet test` passed 191/191 at
  time of review, 192/192 after the fix.
- **Manual AI experiments:** appropriately extensive and already done — `observed-limitations.md` documents
  three full live-corpus runs plus a targeted `--only` run, with genuinely differentiated, falsifiable
  findings (run-to-run non-determinism, the `special-condition` trajectory divergence, three independent
  reproductions of the known crash). This is exactly the mix the milestone calls for: deterministic unit
  tests for the scorer, real runs for everything that can't be unit-tested.

### 📦 Scope Check

- **Does this branch correspond to the current milestone?** Yes — the working-tree changes are entirely
  `Evaluation/`, its tests, `Program.cs`'s `evaluate` command wiring, and `.project-plans/milestone-8/` docs.
- **Does the diff contain only work appropriate to this milestone?** Mostly. The `--from`/`--only` addition
  to `EvalScenarioSelector.cs`/`Program.cs` wasn't in the original plan but is documented as a deliberate,
  narrowly-scoped, user-approved response to a real rate-limit problem found while running the harness — not
  scope creep. The one item worth a deliberate decision is the already-committed `docs/PRD.md` Milestone 8.5
  addition (see Review Notes) — it's about a *future* milestone, not this one's deliverable.
- **Did unrelated changes get mixed into the branch?** No unrelated feature work. See the PRD note above for
  the one borderline case.
- **Did it implement anything from future milestones?** No. No numeric confidence, no LLM-as-judge scoring, no
  source-corpus expansion, no UI work — all correctly deferred per the plan's "Out of Scope" section.
- **Did it introduce unnecessary architecture or dependencies?** No new NuGet packages. `Evaluation/` as a new
  top-level module matches PRD §11's illustrative structure. `EvalScenario.BranchGroup` (speculative,
  never-read) and `ScenarioTrajectory.FinalRetrievedChunks` (redundant with `Turns[^1].RetrievedChunks`) were
  both identified as unnecessary by the milestone's own internal review and have since been removed —
  confirmed by grep: no remaining references in code.
- **Operational scope note:** nothing in the actual Milestone 8 implementation is committed yet (see PR
  Review Summary) — this needs to happen before a PR can be opened, independent of any finding above.

### Final Verdict

**Approve With Minor Comments**

The implementation is sound: genuine reuse of Milestones 5-7, a properly deterministic scorer, real and
honestly-documented live-corpus validation, and a clean response to the internal review's three
Consider-Improving items. The Major issue found has since been fixed and directly regression-tested, and the
Minor issue has been fixed as well (see their notes above). What remains is only the Review Notes (the
bundled Milestone 8.5 PRD commit; the dead `default` switch arm) — neither merge-blocking. Commit the
working-tree changes before opening the actual PR.
