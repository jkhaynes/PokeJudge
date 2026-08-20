# Milestone 8.5 — Implementation Summary

**Milestone:** Milestone 8.5 — Evaluation Dataset Hardening
**Branch:** `milestone/8.5-eval-dataset-hardening`

## Milestone Implemented

Milestone 8.5 — Evaluation Dataset Hardening. Hardens Milestone 8's evaluation dataset and harness before
Milestone 9's calibration work depends on it: expands the dataset from 8 to 20 hand-authored scenarios,
normalizes and actually uses scenario categorization, reviews the original 8 scenarios' expected trajectories
against Milestone 8's real-run findings, adds `--repeat <n>` support so run-to-run variability is observable,
generalizes the scripted judge to support multiple clarification rounds (found necessary by testing the newly
expanded dataset live), distinguishes infrastructure failures from real scenario failures, and adds a
manually-authored source-coverage classification for repeatedly-failing scenarios.

**Scope note added 2026-08-20:** the paragraph above and the "What Changed" section below describe this
milestone's originally-approved scope and were accurate as of the two checkpoint commits (`2c392f7`,
`a8f4fb1`). A follow-up session on this same branch went beyond that approved scope to investigate and fix the
recurring "insufficient with zero questions" sufficiency-assessment crash -- a deliberate, after-the-fact scope
expansion, recorded in full in `plan.md`'s addendum and `five-run-validation.md`'s "Follow-up: 2026-08-20"
section. That work **did** change `Clarification/SystemPrompts.cs`, `Clarification/ClarificationLoop.cs`,
`StructuredState/ClarificationResult.cs`, and `Evaluation/ScenarioEvalScorer.cs` (a new
`ExpectedTrajectoryOutcome.ExpectedUnresolvable` scoring branch) -- directly contradicting the "no change to
`ScenarioEvalScorer`'s scoring criteria" claim above and the "no changes to `Clarification/`... or
`ScenarioEvalScorer.cs`" claim in "What Changed" below. Both claims are left unedited below as an accurate
record of the originally-approved implementation; this note is the correction for anyone reading only the top
of this document.

## What Changed

- **`Evaluation/EvalDataset.cs`** — expanded from 8 to 20 scenarios. 12 new scenarios cover the two
  previously-missing PRD §15 categories (Attack Resolution, Timing Questions) plus meaningfully distinct
  additions to existing categories, one deliberately vague "intentionally incomplete prompt" scenario, and
  two `SufficientOnFirstTurn` scenarios (`gx-attack-twice`, `ace-spec-count`) grounded in fully explicit,
  unconditional rules. Every new scenario's expected section(s) were verified live via `dotnet run -- search`
  before being added, including reading the full source section text, not just the top search hit. The
  original 8 scenarios' categories were normalized (`special-condition` → "Discretion Required",
  `drew-extra-card` → "Gameplay Error") and their expected trajectories were reviewed one at a time against
  `observed-limitations.md`'s findings, with the decision and rationale documented directly in the file's
  header comment (kept `deck-not-shuffled`, `special-condition`, and `drew-extra-card` as-authored; kept
  `spectator-badges` as-authored after investigating it via the new source-coverage classification). Two new
  scenarios (`weakness-not-applied`, `supporter-twice`) initially failed live testing and were corrected —
  see the `ScriptedAnswers` item below.
- **`Evaluation/EvalScenario.cs`** — `ScriptedAnswer: string?` generalized to `ScriptedAnswers:
  IReadOnlyList<string>`, an ordered sequence consumed one per clarifying round. Found necessary after live
  testing: `weakness-not-applied` and `supporter-twice` both exhausted the turn cap because the scripted
  judge had nothing to offer beyond the first round. Investigation (after adding question-text visibility
  below) showed the model was actually asking the *same* question repeatedly, not a sequence of different
  ones — the real fix was correcting each scenario's single answer to address the fact actually being asked
  for, not adding a second round. The multi-answer capability itself is still real and tested (a synthetic
  two-round scenario is covered in `ScenarioEvalRunnerTests`), just not what these two specific scenarios
  needed.
- **`Evaluation/ScenarioEvalRunner.cs`** — `askJudge` now walks an index through `ScriptedAnswers` instead of
  a single-use flag; `AskedMoreQuestionsThanScripted` now means "asked beyond the last scripted answer," not
  "asked more than one round" — scenarios that only ever needed one answer are unaffected.
- **`Program.cs`**'s `RunScenarioEval` — now also prints each turn's actual clarifying-question text and
  `RelatedChunkId` in eval mode (previously silent about this; interactive mode already printed it). This
  visibility gap is what made the `weakness-not-applied`/`supporter-twice` root cause identifiable at all —
  without it, the failure looked like "needed more rounds" when it was actually "the one answer never
  addressed what was asked."
- **`Evaluation/ScenarioCategories.cs`** (new) — the small, allowed category vocabulary every scenario's
  `Category` must come from, enforced by `EvalDatasetTests`.
- **`Evaluation/CategorySummary.cs`** (new) — pure aggregation grouping scored results by category, in
  first-seen order. Wired into `Program.cs`'s `RunScenarioEval`, which now prints a per-category pass/fail
  breakdown — the first thing that actually reads `EvalScenario.Category` for something other than a print
  line (it was print-only in Milestone 8).
- **`Evaluation/EvalScenarioSelector.cs`** — extended with `--repeat <n>` parsing (positive integer, default
  1), returned alongside the existing `--from`/`--only` selection.
- **`Program.cs`**'s `RunScenarioEval` — now loops each selected scenario `--repeat` times, printing every
  run's own outcome (never collapsed) plus a per-scenario "`N/repeat` runs fully passed" aggregate line when
  `repeat > 1`. Catches `HttpRequestException` (the type `GeminiLlmClient`/`GeminiEmbeddingClient` already
  throw for any non-success HTTP response, including a 429) per run, reports it as a distinct
  "infrastructure failure — not counted," and continues rather than crashing the whole command or counting it
  against pass/fail totals.
- **`Evaluation/SourceCoverageFinding.cs`** (new) — `SourceCoverageLevel` enum (Sufficient / RetrievalProblem
  / PossibleSourceGap / ConfirmedSourceGap, matching PRD §15's four definitions) and a record capturing a
  scenario ID, level, notes, and optionally what's missing / the likely source document.
- **`Evaluation/SourceCoverageFindings.cs`** (new) — the actual, populated findings from manually investigating
  four scenarios (`spectator-badges`, `drew-extra-card`, `deck-not-shuffled`, `missed-prize`) by inspecting
  their real-corpus retrieval via `dotnet run -- search` and reading the full source section text. Three
  classified Sufficient coverage; `missed-prize` classified Possible source gap. Written up in full in the new
  `.project-plans/milestone-8.5/source-coverage-analysis.md`.

No new NuGet packages. No changes to `Clarification/`, `Grounding/`, `Retrieval/`, `StructuredState/`, or
`ScenarioEvalScorer.cs` — this milestone hardens the dataset and the harness's orchestration/scripting layer
only, per the approved plan's explicit scope boundary. `ScenarioEvalRunner.cs` was touched only for the
scripted-answer indexing change described above.

**No longer true as of the 2026-08-20 scope expansion noted at the top of this document** — `Clarification/`,
`StructuredState/`, and `ScenarioEvalScorer.cs` were all subsequently modified to fix the zero-question
sufficiency-assessment crash. This paragraph is kept as an accurate record of the state at the two checkpoint
commits it describes.

## Validation

- **Build:** `dotnet build` — succeeded, 0 warnings, 0 errors.
- **Tests:** `dotnet test` — 214/214 passed (192 carried over from Milestone 8, 22 new).
  - **Written first (Red step), all deterministic:** `EvalScenarioSelectorTests` (7 new `--repeat` cases,
    covering valid counts, combination with `--from`/`--only`, zero/negative/non-numeric/missing-value
    errors), `CategorySummaryTests` (5 cases: empty input, single-category pass/mixed, multi-category
    grouping, first-seen-order preservation), `EvalDatasetTests` (5 invariant checks: dataset size in the
    20-30 range, unique IDs, every category in the allowed set, every allowed category used at least once,
    every `RequiresOneClarification` scenario has at least one scripted answer), `SourceCoverageFindingsTests`
    (4 cases: non-empty, every finding references a real scenario ID, no duplicate scenario IDs, every gap
    finding records what's missing), and `ScenarioEvalRunnerTests`
    (`RunAsync_RequiresTwoClarifications_ConsumesBothScriptedAnswersInOrderAndReachesSufficiency`, added after
    generalizing `ScriptedAnswer` to `ScriptedAnswers`, confirming both answers are consumed in order and
    `AskedMoreQuestionsThanScripted` only flips once the model asks beyond both).
  - **Final coverage-review pass:** no additional deterministic logic emerged beyond what was anticipated —
    `Program.cs`'s repeat/infrastructure-failure loop is orchestration coupled to live async calls and
    Console I/O, consistent with this project's established practice of not unit-testing `Program.cs`
    directly.
- **Real-data validation** (see `observed-limitations.md` for full detail): `--repeat 3` on `deck-not-shuffled`
  reproduced its known non-determinism live within a single command (1/3 runs passed); a `--from` run
  selecting the 12 newest scenarios hit the free-tier rate limit hard (12 consecutive 429s from residual
  quota use) and the new infrastructure-failure handling caught every one without crashing, correctly
  excluding them from the pass/fail totals; `gx-attack-twice` passed cleanly on its first live run;
  `weakness-not-applied` and `supporter-twice` initially exhausted the turn cap, were diagnosed with the new
  question-text visibility, corrected, and re-run to confirm both now pass all 6 applicable criteria; a
  regression check on `drew-extra-card` confirmed no behavior change for already-working single-answer
  scenarios.

## Intentional Limitations

- **The dataset remains hand-authored and comparatively small (20 scenarios).** This milestone improves
  coverage, regression detection, and gives Milestone 9 more (and more honestly-labeled) input data, but it
  still does not establish a general system error rate or statistically representative judge behavior — the
  exact limitation Milestone 9's required limitations analysis must grapple with, now with repeated-run
  observations layered on top of raw sample size.
- **Source-coverage classification is a single human judgment call**, not validated against a second
  independent reviewer or any automated check — deliberately not an LLM-as-judge check, consistent with
  Milestone 7's finding that self-validation isn't independent verification.
- **`missed-prize`'s "possible source gap" was not acted on.** Per the plan's explicit "keep source expansion
  deliberate" requirement, no corpus changes were made on the strength of one investigation; it's recorded as
  a candidate for confirmation, not treated as settled.
- **`spectator-badges`'s known gap (sufficiency-assessment declining an inferential connection) remains
  unresolved.** Kept as a documented, known regression check per the milestone's own review framework, not
  silently loosened to force a pass — the same treatment already given the "insufficient with zero questions"
  crash.
- **`ExpectedTrajectoryOutcome.RequiresOneClarification`'s name is now slightly inaccurate** now that a
  scenario can script more than one round — flagged during planning and deliberately left as-is rather than
  renamed, since the rename is cosmetic, ripples through every scenario/test, and wasn't part of the approved
  fix's scope.

## Learning Focus

- **Distinguishing "the model was non-deterministic" from "the scenario's expectation was wrong,"** applied
  for real: `deck-not-shuffled` and `special-condition`'s divergences were kept as evidence-grounded
  expectations rather than loosened, while `spectator-badges`'s *repeatable* mismatch was investigated rather
  than assumed to mean the same thing as the others.
- **Root-cause attribution in a RAG system, demonstrated with real evidence rather than asserted:** three of
  four investigated scenarios turned out to be Sufficient coverage (the corpus has the answer; the gap is in
  PokéJudge's reasoning), while one (`missed-prize`) turned out to be a genuine possible gap in the corpus
  itself — two different problems that would call for two different fixes, distinguishable only by actually
  reading the retrieved material against the real source text.
- **Where non-determinism handling belongs in a layered system, tested under real pressure rather than just
  designed:** repeated-run support and infrastructure-failure handling both live entirely in `Program.cs`'s
  orchestration layer; `ScenarioEvalScorer` was never touched, and a real, unplanned rate-limit event (12
  consecutive 429s) confirmed that boundary holds up in practice, not just on paper.
- **A field that's captured but never consumed is a real defect, not a stylistic nitpick** — reinforced by
  deliberately avoiding the exact mistake Milestone 8's own review caught with `BranchGroup`:
  `SourceCoverageFinding`/`SourceCoverageFindings` were built as actual, populated, tested data from the
  start, not a shape awaiting future use.
- **A symptom is not a diagnosis.** "Needed more clarification than scripted" looked, from the pass/fail
  output alone, like evidence that these scenarios structurally required multiple rounds. The real cause —
  the model asking the identical question four times because one answer never addressed it — was only
  visible once the actual question text was printed. Eval mode had been silently missing exactly the
  visibility interactive mode already had; the general lesson (print what the model is actually doing before
  theorizing about why it's failing) is worth carrying into any future eval debugging, not just this one fix.

## What I Should Try

1. Run `dotnet run -- evaluate --only weakness-not-applied` and `--only supporter-twice` again yourself, ideally
   spaced a few minutes apart to clear the rate limit — both now pass consistently in testing, but confirm
   for yourself and see the printed question text directly.
2. Run `dotnet run -- evaluate --repeat 5` against `special-condition` (the other scenario with a documented
   real divergence) and see whether its own zero-clarification run from Milestone 8 reproduces here too.
3. Read `.project-plans/milestone-8.5/source-coverage-analysis.md` and decide for yourself: does `missed-prize`'s
   "possible source gap" finding change how you'd interpret its `ExpectedToFailLoudly` regression check —
   should a future milestone confirm the gap before assuming the crash is purely a reasoning bug?
4. Try running the full 20-scenario dataset in one sitting (accepting it will likely need `--from` to resume
   across the rate limit, per Milestone 8's own documented constraint) and see how the new per-category
   summary reads once every category has real data in it.

## Git Status

- **Branch:** `milestone/8.5-eval-dataset-hardening`
- **Uncommitted:** yes — all implementation changes are in the working tree, nothing staged or committed yet
  (this skill does not commit automatically).
- **Unexpected files:** none. `git status` shows exactly the expected changes: modified `Program.cs`,
  `Evaluation/EvalDataset.cs`, `Evaluation/EvalScenario.cs`, `Evaluation/EvalScenarioSelector.cs`,
  `Evaluation/ScenarioEvalRunner.cs`, and three test files
  (`EvalScenarioSelectorTests.cs`, `ScenarioEvalRunnerTests.cs`, `ScenarioEvalScorerTests.cs`); new
  `Evaluation/CategorySummary.cs`, `Evaluation/ScenarioCategories.cs`, `Evaluation/SourceCoverageFinding.cs`,
  `Evaluation/SourceCoverageFindings.cs`, three new test files (`CategorySummaryTests.cs`,
  `EvalDatasetTests.cs`, `SourceCoverageFindingsTests.cs`), and new `.project-plans/milestone-8.5/` documents
  (`plan.md`, `observed-limitations.md`, `source-coverage-analysis.md`, this file).
