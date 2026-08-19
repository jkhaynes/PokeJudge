# Milestone 8 Review

**Milestone reviewed:** Milestone 8 — Evaluation
**Plan:** `.project-plans/milestone-8/plan.md`
**Branch:** `milestone/8-evaluation`
**Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
**Tests:** `dotnet test` — 187/187 passed at time of review; 191/191 after the fixes below (4 new tests
for the "Answer budget" criterion; the `BranchGroup` and `FinalRetrievedChunks` removals needed no new
tests — pure data-shape simplifications with no behavior change).

**Update:** All three Consider-Improving items have since been addressed — see each item's note below.

## ✅ Matches the Plan

- **All planned deliverables present**: `Evaluation/EvalScenario.cs`, `EvalDataset.cs`, `ScenarioTrajectory.cs`,
  `ScenarioEvalRunner.cs`, `ScenarioEvalScorer.cs`, the `dotnet run -- evaluate` command, tests, and
  `observed-limitations.md` all exist and match what the plan called for.
- **Genuine reuse, not reinvention** — verified by reading each piece directly: `ScoreInitialRetrieval` and
  `ScorePostAnswerRetrieval` both call `RetrievalEvaluator.Evaluate` (Milestone 5) unchanged;
  `ScenarioEvalRunner` drives the real, unmodified `ClarificationLoop`/`RulingGenerator`/`GroundingValidator`
  (Milestones 6-7); `GroundingResult`/`SourceSupport` are consumed as-is. No new retrieval, grounding, or
  generation logic was introduced anywhere in this diff.
- **Branching scenarios correctly use a flat representation**, exactly as planned — confirmed by reading
  `EvalDataset.cs`: `special-condition` is one row with `ScriptedAnswer` set, not a nested structure. Matches
  PRD §18's explicit allowance for a flat list.
- **The malformed-response crash path is handled correctly and deliberately**, not accidentally swallowed:
  `ScenarioEvalRunner` catches only `InvalidOperationException` (the exact type Milestone 2's guard throws),
  letting any other exception type propagate and crash the harness for real — verified by reading the catch
  clause. A scenario expecting this failure (`missed-prize`) scores it as a pass; a scenario not expecting it
  correctly reports "Unexpected failure" instead of silently absorbing it into unrelated criteria (see below).
- **Dataset entries are evidence-grounded, not guessed.** Spot-checked several against the milestones they
  cite: `repeat-violations` targeting `PPG-4.2.2` matches the real retrieval result documented in Milestone
  7's ingestion-branch PR review; `special-condition`'s accepted `{Strong, Partial}` set is a deliberate,
  explained choice tied to Milestone 7's real Strong/Partial divergence, not an arbitrarily loose range.
- **The real run was actually run, repeatedly, and the results were used.** Three full attempts plus a
  targeted `--only` run are documented with specific, differentiated outcomes per scenario (not a single
  generic "some scenarios failed" summary) — `observed-limitations.md` is concrete and falsifiable, not vague.
- **A real bug found by running the harness was fixed with a regression test**, not just patched ad hoc:
  `ScenarioEvalScorer`'s original version conflated "unexpected crash" with "asked a clarifying question" for
  non-`ExpectedToFailLoudly` scenarios; the fix adds an explicit early-return branch, and
  `Score_NotExpectedToFailButDidAnyway_...` locks it in.
- **The `--from`/`--only` addition is correctly and narrowly scoped.** Confirmed by reading
  `EvalScenarioSelector.cs` and its usage in `Program.cs`: it touches only `Evaluation/`/`Program.cs`, leaves
  `GeminiLlmClient` untouched, and its own reasoning (developer/CI tooling vs. the judge-facing product, which
  never needs retry/backoff) is sound and explicitly recorded in code comments, not just chat history.

## 🚨 Must Fix

None.

## ⚠️ Consider Improving

- ~~**`ScenarioTrajectory.AskedMoreQuestionsThanScripted` is captured and unit-tested at the runner level, but
  never actually scored or surfaced anywhere a developer would see it.** (`Evaluation/ScenarioEvalScorer.cs`,
  `Program.cs`'s `RunScenarioEval`) Confirmed by grep: the field is set in `ScenarioEvalRunner` and asserted
  directly in `ScenarioEvalRunnerTests`, but `ScenarioEvalScorer.Score` never reads it, and
  `RunScenarioEval`'s console output never prints it. This matters because the plan's own design intent, and
  the runner's own code comment, explicitly frame this as "a real, informative outcome to record **and
  score**" — it's recorded, but not scored. Concretely: `spectator-badges`'s real run asked multiple
  unscripted clarifying rounds before exhausting the turn cap, which is directly relevant context for why it
  failed, and that fact is currently invisible in both the eval report and the console output. Recommended
  direction: either add a criterion (e.g., "Answer budget" — fail if a scenario needed more clarification
  than scripted, at least when it still reached sufficiency) or, at minimum, print the flag in
  `RunScenarioEval`'s per-scenario output so it's visible even without a formal criterion.~~ **Fixed.** Added
  `ScoreAnswerBudget` to `ScenarioEvalScorer.cs`, a new "Answer budget" criterion, scoped to
  `RequiresOneClarification` scenarios only (as recommended — for `SufficientOnFirstTurn`, any question
  already fails the existing "Sufficiency timing" criterion, so a separate one there would be redundant).
  `RunScenarioEval` now also prints `Asked more questions than scripted: ...` per scenario regardless of
  criterion applicability. Locked in with 4 new table-driven tests (fail/pass cases plus criterion-omitted
  checks for `SufficientOnFirstTurn`/`ExpectedToFailLoudly`), and re-verified live against both real
  `RequiresOneClarification` scenarios (`special-condition`, `drew-extra-card`), which both correctly scored
  "Answer budget: Pass".
- ~~**`EvalScenario.BranchGroup` is written throughout the dataset and tests but never read anywhere.**
  (`Evaluation/EvalScenario.cs`, `EvalDataset.cs`) The type's own doc comment says branch rows share a
  `BranchGroup` "for reporting" purposes, but `RunScenarioEval`'s console output doesn't group or label
  scenarios by it, and no scoring logic reads it either. Compounding this, `EvalDataset` currently has exactly
  one scenario (`special-condition`) using a non-null `BranchGroup`, with no sibling row sharing it — so even
  if grouping were implemented, there's nothing in the current dataset to group. Low-risk (it's inert, not
  wrong), but worth either wiring it into the console output when a real multi-branch family gets added, or
  removing it now and reintroducing it when actually needed (YAGNI).~~ **Fixed — removed (YAGNI).** Deleted
  the field from the `EvalScenario` record and its "for reporting" doc-comment reference, every call site in
  `EvalDataset.cs`, and every call site in `ScenarioEvalScorerTests`, `ScenarioEvalRunnerTests`, and
  `EvalScenarioSelectorTests`. Nothing scored or reported on it, so removal was zero-risk — confirmed by the
  full suite passing unchanged.
- ~~**`ScenarioTrajectory.FinalRetrievedChunks` duplicates data already available via `Turns`.** Confirmed by
  tracing `ScenarioEvalRunner.RunAsync`: `finalChunks` is assigned from the same `lastRetrievedChunks`
  variable that also produced the final `TurnRecord.RetrievedChunks` — the two are always identical in
  practice. Nothing in `ScenarioEvalScorer` or `Program.cs` reads `FinalRetrievedChunks` directly. Not
  incorrect, just a redundant field that could be dropped in favor of `trajectory.Turns[^1].RetrievedChunks`
  without losing information.~~ **Fixed.** Removed the parameter from `ScenarioTrajectory`'s record
  declaration and from the `Failed`/`TurnCapExhausted`/`Completed` factory methods; updated
  `ScenarioEvalRunner.RunAsync`'s call to `Completed(...)` to drop it (the local `finalChunks` variable is
  unchanged — it's still needed for the `RulingGenerator`/`GroundingValidator` calls, just no longer threaded
  into the trajectory). No test referenced `FinalRetrievedChunks` directly. Full suite re-run: 191/191, zero
  behavior change, exactly the zero-risk removal predicted.

## 🔧 Fix Plans for Consider-Improving Items

Not implemented as part of this review — plans only, for whenever this follow-up is picked up. **All three
have since been carried out; see the ✅ Consider Improving notes above for what was actually done.** Kept
below as the original plan, for reference.

### 1. Score/surface `AskedMoreQuestionsThanScripted`

1. Add a new private method to `Evaluation/ScenarioEvalScorer.cs`, `ScoreAnswerBudget(EvalScenario scenario,
   ScenarioTrajectory trajectory)`, returning a `CriterionOutcome` named `"Answer budget"`: `Fail` when
   `trajectory.AskedMoreQuestionsThanScripted` is `true`, `Pass` otherwise.
2. Call it from `Score()` alongside the other `RequiresOneClarification`-only criteria (next to
   `ScoreClarifyingQuestionMateriality`/`ScorePostAnswerRetrieval`) — scope it to `RequiresOneClarification`
   only, not `SufficientOnFirstTurn`: for `SufficientOnFirstTurn` scenarios, any question at all already fails
   the existing `Sufficiency timing` criterion, so a separate answer-budget criterion would be redundant
   there. It only adds new information for scenarios that scripted exactly one answer and then needed more.
3. Add table-driven tests in `ScenarioEvalScorerTests`: `AskedMoreQuestionsThanScripted: true` → `Fail`;
   `false` → `Pass`; confirm the criterion is absent for `SufficientOnFirstTurn`/`ExpectedToFailLoudly`
   scenarios (mirroring how `ScoreClarifyingQuestionMateriality`'s applicability is already tested).
4. Regardless of the criterion, also print `trajectory.AskedMoreQuestionsThanScripted` in `Program.cs`'s
   `RunScenarioEval` per-scenario output (e.g., alongside the existing `Turns used: ...` line) so it's visible
   even for scenario types the new criterion doesn't cover.
5. Re-run `dotnet run -- evaluate --only special-condition` and `--only drew-extra-card` (the two
   `RequiresOneClarification` scenarios) after the change to confirm the new criterion behaves as expected
   against live data, not just fixtures.

### 2. `EvalScenario.BranchGroup`

Two viable directions; pick one rather than leaving it as-is:

- **Remove now (YAGNI), reintroduce when needed.** Delete the field from `EvalScenario`, its constructor
  argument at every call site in `EvalDataset.cs` and the test files, and the "for reporting" doc-comment
  reference in `EvalScenario.cs`. Cheapest option, and consistent with this project's general practice of not
  carrying speculative fields. Straightforward since nothing currently reads it — no scoring or runner logic
  to update.
- **Wire it into reporting**, only if/when a real multi-branch family (e.g., a genuine Yes/No pair sharing one
  `InitialDescription`) is added to `EvalDataset`: group `RunScenarioEval`'s console output by `BranchGroup`
  (print grouped scenarios under one shared heading, or annotate each with its sibling branch's result) so a
  developer can see both branches of the same decision point together rather than scattered through the full
  scenario list.

Recommend removal now, since the dataset has no scenario pair to group today — reintroducing a single field
later is cheap, and the current state (unused since the milestone was implemented) is exactly the sign YAGNI
exists to catch.

### 3. `ScenarioTrajectory.FinalRetrievedChunks`

1. Remove the `FinalRetrievedChunks` parameter from `ScenarioTrajectory`'s record declaration and from the
   `Failed`/`TurnCapExhausted`/`Completed` factory methods' signatures and bodies.
2. Update `ScenarioEvalRunner.RunAsync`'s call to `ScenarioTrajectory.Completed(...)` to drop the now-removed
   argument.
3. Confirm no test references `trajectory.FinalRetrievedChunks` directly (a quick grep first) before removing
   — if any do, repoint them at `trajectory.Turns[^1].RetrievedChunks`, which carries the same data.
4. Re-run the full test suite to confirm no behavior change (this is a pure data-shape simplification, not a
   logic change — should be a zero-risk removal).

## 🧪 Learning Observations

- **The `special-condition` divergence (`observed-limitations.md` §3) is the single best piece of evidence
  this milestone produced**, and it happened by observation, not design: a run that resolved with zero
  clarifying questions still passed on final Source Support alone, while three trajectory-level criteria
  correctly failed. This is PRD §15's core argument for trajectory evaluation, demonstrated with a real,
  unscripted run rather than only asserted. Worth understanding deeply before moving on — it's the clearest
  illustration in this whole project so far of why "the final answer looked right" is an insufficient
  correctness bar for a multi-turn system.
- **Real, run-to-run non-determinism in the model's own sufficiency behavior** (`deck-not-shuffled` behaving
  three different ways across three identical-input runs) is a genuinely important finding for interpreting
  *any* single eval run's pass/fail result, this milestone's or a future one's. It directly complicates the
  idea that "the eval suite passed" is a stable, repeatable claim about the system, and it's honestly
  documented as such rather than treated as noise to explain away.
- **Three independent scenario categories (prize errors, illegal game state, gameplay error) now reproduce
  the identical malformed-response crash** first observed in Milestone 6. This is a meaningfully stronger
  signal than Milestone 6's single anecdote — worth treating as a real priority for whichever future milestone
  next touches the sufficiency-assessment prompt, not just a recurring curiosity.
- **The free-tier rate-limit finding, and the `--from`/`--only` response to it, is itself a small but genuine
  lesson in matching infrastructure to actual need.** The instinct to reach for retry/backoff in the shared
  LLM client was correctly rejected once it was established that the judge-facing product never needs it —
  the fix stayed exactly as large as the problem (a manual resume flag for dev tooling), not larger.

## 🎯 Learning Objective Check

1. **What AI concept was this milestone intended to teach?** Evaluation as a discipline distinct from manual
   testing; trajectory (process) evaluation specifically — scoring the investigation, not just the
   destination.
2. **Does the implementation expose that concept clearly?** Yes, unusually clearly for this project — the
   `special-condition` run is a real, unplanned case where trajectory scoring and destination scoring
   disagreed, which is a more convincing demonstration than a scenario built to always pass every criterion
   together would have been.
3. **What should the developer be able to explain after completing this milestone?** Why a captured
   trajectory (not a live model call) is what gets scored, and why that comparison is fully deterministic even
   though the trajectory itself isn't; the concrete mechanism behind the `special-condition` divergence; why
   this milestone deliberately did not build an LLM-as-judge quality scorer; and why `--from`/`--only` was the
   right-sized fix for a rate limit that only affects developer tooling, not the product.
4. **Is any abstraction hiding something the developer should understand directly?** Mostly no — the
   trajectory/scorer split keeps everything inspectable, and the console output prints per-criterion detail
   rather than a bare pass/fail. The one partial exception is `AskedMoreQuestionsThanScripted` (see Consider
   Improving): it's captured faithfully but currently invisible downstream, which means a developer reading
   only the eval report — not the code — could miss a real signal the harness already has in hand.

## 📋 Plan Completion

| Step | Status |
|---|---|
| 1. Add `Evaluation/EvalScenario.cs` | Complete |
| 2. Add `Evaluation/EvalDataset.cs` | Complete |
| 3. Add `Evaluation/ScenarioEvalRunner.cs` | Complete |
| 4. Add `Evaluation/ScenarioEvalScorer.cs` | Complete |
| 5. Wire `dotnet run -- evaluate` | Complete — later extended with `--from`/`--only`, a user-approved, narrowly-scoped addition made after real usage surfaced the rate-limit problem |
| 6. Run the real harness against the real corpus and capture the output | Complete — three full attempts plus one targeted run, honestly documented including partial completion |
| 7. Update/add tests | Complete |
| 8. Document observed findings in `observed-limitations.md` | Complete, and unusually substantive — includes a real scorer bug found live, a genuine divergence case, and three independent reproductions of a known failure mode |

## Final Verdict

**Ready to Complete**

No blockers or major issues. The implementation matches the approved plan, reuses Milestones 5-7's building
blocks correctly, and produced real, well-documented evidence for its core learning objective — including a
divergence case that makes PRD §15's trajectory-evaluation argument more convincingly than the plan's own
worked example did. The three Consider-Improving items are all "captured but not fully used" data-modeling
gaps, not correctness defects — worth a follow-up pass, but none of them affect the accuracy of any eval
report produced so far, and none should block considering this milestone done.
