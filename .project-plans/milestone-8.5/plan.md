# Milestone 8.5 — Evaluation Dataset Hardening

Status: planned, not started
Source: PRD.md §15 ("Evaluation dataset hardening (Milestone 8.5)" subsection), §14 roadmap table row `8.5`,
Learning Objectives → "Milestone 8.5"

## 1. What We Will Build

Milestone 8 built the measurement layer; Milestone 8.5 hardens what it measured *with*. Running the harness
for real surfaced two problems that make the current 8-scenario dataset a shakier foundation than Milestone
9's calibration work needs: the dataset is small, and at least one scenario (`deck-not-shuffled`) produced
three different outcomes across three identical live runs — a single observed run is not a stable
ground-truth label. This is a hardening pass over the existing harness and dataset, not a new evaluation
capability or a benchmark-building project:

1. **Expand the dataset** from 8 to roughly 20-30 hand-authored scenarios, closing the category gaps the
   current dataset has against PRD §15's required list (notably "incorrect attack resolution" and "timing
   questions," neither currently represented) and adding real breadth within already-covered categories —
   not near-duplicates of the same 8 issues.
2. **Formalize and actually use scenario categorization.** `EvalScenario.Category` already exists, but it's
   currently print-only (`RunScenarioEval` prints it per scenario and never aggregates it) — the same
   "captured but never consumed" gap Milestone 8's own review flagged for the `BranchGroup` field it later
   removed. This milestone normalizes the category values (the current 8 already have overlapping/inconsistent
   naming — e.g. `"Illegal Game State"` vs. `"Illegal Game State / Discretion Required"`) and wires them into
   a per-category summary so a developer can actually ask "are failures concentrated in one category?"
3. **Review, not blindly trust, the original 8 scenarios' expected trajectories** against what
   `observed-limitations.md` actually recorded — deciding case by case whether a divergence was genuine model
   non-determinism (expectation stays), a scenario that may have more than one valid investigation path
   (expectation stays, documented as such), or a real sign the original expectation deserves reconsideration.
4. **Add repeated-run support** to `dotnet run -- evaluate` so run-to-run variability is observed rather than
   hidden behind whichever single run happened to occur — e.g., being able to say "`deck-not-shuffled`
   succeeded in 2 of 5 observed runs," with every individual run still separately inspectable.
5. **Add a lightweight source-coverage classification** (Sufficient coverage / Retrieval problem / Possible
   source gap / Confirmed source gap) for scenarios that repeatedly fail or behave unexpectedly, to
   distinguish a retrieval/corpus problem from a reasoning/clarification/grounding problem instead of
   attributing every failure to the LLM by default.
6. **Distinguish infrastructure failures from scenario failures.** Currently, a rate-limit response
   (`HttpRequestException`, the type `GeminiLlmClient` already throws for any non-success HTTP status) would
   propagate uncaught out of the harness and crash the entire `evaluate` run — indistinguishable, at the
   process level, from any other failure. With an expanded dataset and repeated runs multiplying real API
   call volume, this needs to be a recognized, non-scored outcome, not a silent process crash.

## 2. AI Concepts Being Learned

- **Evaluation-dataset curation as its own discipline, separate from building the harness that runs it.**
  Milestone 8 proved the harness works; this milestone asks whether it's pointed at good enough data — a
  distinct, and often under-appreciated, part of building any evaluation system.
- **Distinguishing "the model behaved non-deterministically" from "the eval scenario's expectation was
  wrong."** These look identical from a single failing run — a repeatable pattern across the expanded
  dataset's `--repeat` runs is what actually tells them apart. Conflating them either direction corrupts
  future milestones' ground truth: treating instability as a wrong label loses a real finding; treating a
  wrong label as instability keeps a broken regression check around.
- **Root-cause attribution in a RAG system, made concrete.** A scenario failure can originate in retrieval
  (the corpus has the answer, retrieval didn't surface it), the corpus itself (the answer genuinely isn't
  there), or the model's reasoning over what it was given — three different problems with three different
  fixes, and Milestone 8 alone never had to tell them apart.
- **Where non-determinism handling belongs in a layered system, reinforced.** Repeated-run support is
  designed to live entirely in the harness/orchestration layer (`Program.cs`), not in `ScenarioEvalScorer` —
  the same non-determinism/determinism boundary Milestone 8 established stays intact; this milestone tests
  whether that boundary actually holds up under a new kind of pressure (running the same scorer many times
  over) rather than redrawing it.

## 3. Implementation Steps (in order)

1. **Expand `Evaluation/EvalDataset.cs`** to roughly 20-30 scenarios. Add scenarios for the two currently
   uncovered PRD §15 categories (incorrect attack resolution, timing questions), plus meaningfully distinct
   additions within already-covered categories (deck/decklist issues, tournament procedure, penalty
   questions, illegal game state, gameplay error, prize errors, discretion-required) and at least one
   deliberately sparse/incomplete-prompt scenario per PRD §15's explicit "intentionally incomplete prompts"
   category. Verify each new scenario's expected section(s) against the real corpus via `dotnet run --
   search` before adding it — the same evidence-based practice `drew-extra-card` already established, not
   guessed at.
2. **Normalize `Category` values** across the full dataset (existing 8 plus new scenarios) into a small,
   consistent set, and add a per-category pass/fail summary to `RunScenarioEval`'s console output in
   `Program.cs` — the first thing that actually *reads* `Category` for something other than a print line.
3. **Review the original 8 scenarios' expected trajectories** against `observed-limitations.md`'s recorded
   findings, one at a time, and document the decision (kept vs. revised, and why) directly in
   `EvalDataset.cs`'s comments:
   - `deck-not-shuffled`, `special-condition`: keep as authored — both scenarios' expectations are
     independently evidence-grounded from Milestones 6-7's real runs; the observed divergences are model
     non-determinism, not wrong labels.
   - `drew-extra-card`: keep `RequiresOneClarification` — its expected section (`PPG-5.5.1`) was corpus-
     verified before authoring, and its repeated crash is evidence the known sufficiency-assessment bug
     persists, not evidence the expectation is wrong. Downgrading it to `ExpectedToFailLoudly` would quietly
     convert a real regression signal into a tautology.
   - `spectator-badges`: the one scenario with a *repeatable* (not one-off) mismatch — both real runs that
     reached it failed the same way. Investigate with the new source-coverage classification (step 5) before
     deciding whether to revise its expected outcome or keep it as a documented, known gap.
4. **Add repeated-run support**: extend `EvalScenarioSelector`'s parsing with a `--repeat <n>` flag. Each
   repeat drives the existing `ScenarioEvalRunner`/`ScenarioEvalScorer` pipeline independently — no changes to
   either class — and `Program.cs` prints each run's own outcome plus a per-scenario aggregate line (e.g.
   "2/5 runs fully passed") without collapsing the individual results.
5. **Add `Evaluation/SourceCoverageFinding.cs`**: a `SourceCoverageLevel` enum (`Sufficient` /
   `RetrievalProblem` / `PossibleSourceGap` / `ConfirmedSourceGap`, matching PRD §15's four definitions) and a
   small record capturing a scenario ID, its level, and notes on what appears missing (and, for a confirmed
   gap, the likely source document) — following the same typed-classification pattern `SourceSupport` already
   established. This records a *human* judgment call (inspecting retrieved chunks against the real corpus,
   asking whether a knowledgeable human judge would find them sufficient) — not a new automated scoring
   criterion, and deliberately not an LLM-as-judge check, consistent with this project's standing position on
   self/model validation.
6. **Distinguish infrastructure failures**: catch `HttpRequestException` around each per-scenario,
   per-repeat run in `Program.cs`'s orchestration loop, print it as a distinct "infrastructure failure — not
   counted" outcome, and continue to the next run/scenario rather than crashing the whole `evaluate` command
   or letting it silently count against pass/fail totals. `ScenarioTrajectory`/`ScenarioEvalScorer` are
   untouched — this exception never reaches them.
7. **Manually investigate and record source-coverage findings** for the dataset's currently-known,
   repeatedly-failing scenarios (`missed-prize`, `drew-extra-card`, `spectator-badges`, and
   `deck-not-shuffled`'s crash runs): inspect each one's actually-retrieved chunks against the real source
   documents, classify each with the new enum, and write up the findings in a new
   `.project-plans/milestone-8.5/source-coverage-analysis.md`.
8. **Run the expanded harness for real** against the real corpus, including `--repeat` on at least the
   scenarios with already-known run-to-run variability (`deck-not-shuffled`, `special-condition`), managing
   the free-tier rate limit with `--from`/`--only`/`--repeat` combined as needed.
9. **Update/add tests**: `EvalScenarioSelectorTests` for `--repeat` (parsing, validation, interaction with
   `--from`/`--only`); any new tests the per-category summary logic needs. `ScenarioEvalScorerTests` and
   `ScenarioEvalRunnerTests` should need no changes, since neither the scorer's criteria nor the runner's
   per-run behavior changes.
10. **Document findings** in `observed-limitations.md` (per-category patterns if any emerge, actual
    repeated-run variability observed across the expanded dataset) and `implementation-summary.md`.

## 4. Expected Limitations / Failures to Intentionally Observe

- **Even 20-30 scenarios remains small and hand-authored.** This milestone improves coverage and regression
  detection; it does not, and cannot, establish a general system error rate or statistically representative
  judge behavior — worth restating plainly rather than letting the larger number imply more rigor than
  exists, and carried directly into Milestone 9's required limitations analysis.
- **Source-coverage classification is a single human judgment call**, not validated against a second
  independent reviewer or any automated check — the same "who validates the validator" caveat this project
  applied to LLM self-validation in Milestone 7, now applied to a human judgment instead of a model one, but
  still a single point of possible error worth naming.
- **Repeated runs multiply real API cost and the free-tier rate-limit problem Milestone 8 already
  documented.** `--repeat 5` across an expanded ~20-30 scenario dataset is a materially larger real-world run
  than anything Milestone 8 attempted; expect to hit the rate limit again even with `--from`/`--only`/
  `--repeat` combined thoughtfully — worth observing directly rather than assuming the existing tooling fully
  solves it at this larger scale.
- **Reviewing expected trajectories risks quietly rationalizing away a real bug as "just non-determinism."**
  Step 3's per-scenario reasoning is deliberately conservative (keep evidence-grounded expectations, don't
  loosen them just to make the harness pass more often) specifically to guard against this failure mode.

## 5. What I Should Understand by the End

- Why growing an eval dataset is a distinct skill from building the harness that runs it — the difference
  between "the tool works" and "the tool is pointed at good enough data."
- How to tell "the model behaved non-deterministically" apart from "the eval scenario's expectation was
  wrong," and why conflating them corrupts ground truth for every milestone built on top of this dataset.
- Why source-coverage attribution (retrieval/corpus problem vs. reasoning problem) has to happen before
  deciding whether a fix belongs in retrieval, chunking, ingestion, or the sufficiency/ruling prompts.
- Why repeated-run infrastructure belongs in the harness/orchestration layer and not in the deterministic
  scorer, and why that boundary held up (or didn't) once actually put under the pressure of running the same
  scorer many times over.
- Why source-corpus expansion stays explicitly out of scope even when a confirmed source gap is found —
  consistent with this project's pattern of not solving a problem before it's demonstrated to be worth
  solving.

## Out of Scope for This Milestone

- Broad source-corpus expansion — only narrowly justified, small additions if a gap is clearly confirmed; a
  larger expansion is a separate, evidence-based follow-up decision.
- Any change to `ScenarioEvalScorer`'s scoring criteria themselves — this milestone hardens the *dataset* and
  *harness*, not the scoring rubric Milestone 8 already built and tested.
- LLM-as-judge scoring of anything, including source-coverage classification — stays a human judgment call,
  consistent with Milestone 7's and Milestone 8's findings about self-validation.
- Numeric/calibrated confidence scores or any Milestone 9 calibration work — this milestone only prepares the
  dataset Milestone 9 will consume.
- General recursive/multi-level branching-scenario trees — still out of scope, unchanged from Milestone 8's
  own scoping decision.
- A persisted, historical eval-report/dashboard system — `observed-limitations.md` and the new
  `source-coverage-analysis.md` continue to fill PRD §15's "simple run log" role.
- Any UI work (Milestone 10).
