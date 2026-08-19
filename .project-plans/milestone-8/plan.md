# Milestone 8 — Evaluation

Status: planned, not started
Source: PRD.md §15 (Testing and Evaluation Strategy, incl. "Branching / trajectory evaluation"), §18
(branching-scenario representation deferred here), "Learning Objectives → Milestone 8"

## 1. What We Will Build

Every milestone so far has been validated by hand: run a few real scenarios, read the console output, write
down what happened in `observed-limitations.md`. That has been the right call each time (a rigorous harness
before there was a real pipeline to evaluate would have been premature), but PRD §15 is explicit that manual
spot-checking is not the same discipline as evaluation — "results should be reviewable over time... so
regressions are visible as prompts/retrieval change," and outcomes need to be scored against **measurable**
criteria, not just read and judged in the moment.

Milestone 8 does not add a new AI capability. It adds a measurement layer over the capabilities Milestones
1-7 already built:

1. **A hand-authored scenario dataset** covering PRD §15's required categories: missed game actions,
   incorrect attack resolution, illegal game states, drawing too many cards, prize errors, deck/decklist
   issues, timing questions, tournament procedure, penalty questions, discretion-required scenarios, and
   intentionally incomplete prompts. Each scenario states, where applicable, its expected relevant source
   section(s) and an expected/acceptable final outcome.
2. **A subset of those scenarios are branching (trajectory) scenarios** — PRD §15's own example: "Player A
   forgot to take a Prize card" branches on "Was a Pokémon Knocked Out?" into materially different relevant
   policy, follow-up questions, and expected outcomes per branch. We already have two real, well-understood
   candidates sitting in this project's history: the missed-Prize scenario (Milestones 6-7) and the Special
   Condition scenario (Milestones 6-7, including Milestone 7's real Strong/Partial divergence). Both become
   formal, hand-authored eval cases here instead of one-off manual runs.
3. **A harness that runs the real, full pipeline** — retrieve → assess → clarify → re-retrieve → generate →
   validate grounding — against each scenario, using the actual `ClarificationLoop`, `RulingGenerator`, and
   `GroundingValidator` this project already built. No new AI mechanism is introduced; the harness drives
   existing components with a *scripted* judge (pre-authored answers for branching scenarios) instead of a
   human typing at a console.
4. **Deterministic scoring functions** comparing what actually happened against each scenario's expected
   criteria — PRD §15's metrics list, made concrete:
   - Did the system correctly recognize when facts were missing (sufficiency didn't fire prematurely)?
   - Was a clarifying question's `RelatedChunkId` actually tied to a section the scenario names as material
     (not just "was *a* question asked," but "was it asked for the right reason")?
   - Did the final ruling's cited chunks and validated `SourceSupport` fall within the scenario's acceptable
     set (reusing Milestone 7's `GroundingValidator`/`SourceSupportAssigner` as-is)?
   - Retrieval quality at each turn, reusing Milestone 5's `RetrievalEvaluator.Evaluate` directly — it already
     takes any `(query, expectedSectionId)` pair and a result list, with zero coupling to how that pair was
     produced.
5. **A `dotnet run -- evaluate` command**, separate from Milestone 5's existing `dotnet run -- eval`
   (retrieval-only, cheap, unchanged), printing a per-scenario pass/fail breakdown and a summary — the "simple
   run log" PRD §15 asks for. This milestone's `observed-limitations.md` captures a real run's output for
   long-term comparison, the same way every prior milestone's has, rather than building a persisted
   report-history system.

### Reusing, not duplicating, what already exists

The harness's job is orchestration and scoring, not a new pipeline. It calls the real `ClarificationLoop`,
`RulingGenerator`, and `GroundingValidator` unchanged; it reuses `RetrievalEvaluator.Evaluate` unchanged for
retrieval scoring; it reuses the `SourceSupport` enum and `GroundingResult` shape from Milestone 7. Nothing
about Milestones 1-7's production code changes because Milestone 8 exists to measure it, not modify it.

## 2. AI Concepts Being Learned

- **Evaluation as a discipline distinct from manual testing.** A hand-built dataset with explicit expected
  criteria, scored programmatically, is a different kind of artifact than "I ran it and it looked right" —
  it's repeatable, comparable across runs, and forces the criteria to be written down *before* looking at the
  output.
- **Trajectory (process) evaluation.** For a multi-turn, branching system, the final answer being correct is
  not sufficient evidence the system worked correctly — PRD §15's own framing: a lucky correct ruling reached
  via a wrong investigation path (skipped a necessary clarification, asked an irrelevant question, retrieved
  the wrong policy, then landed on the right answer anyway) must be distinguishable from one reached via a
  correct investigation. This milestone scores intermediate decisions (sufficiency timing, question
  materiality, branch chosen, per-branch retrieval) alongside the destination.
- **What can be scored deterministically versus what still can't**, at the evaluation layer this time (a
  direct continuation of Milestone 7's deterministic-vs-model-judgment split). Structural checks — does a
  cited section ID match an expected one, does a validated Source Support fall in an acceptable set, did
  sufficiency fire at the right turn — are all fully automatable. Whether a ruling's prose is actually *good*
  is not attempted here; that would require either a human reviewer or an LLM-as-judge approach, and this
  project has already spent Milestone 7 establishing why LLM-judging-LLM output needs to be treated carefully,
  not adopted casually as a shortcut to "automated quality scoring."
- **Why a small, hand-authored dataset is a real, named limitation, not a temporary inconvenience** — this
  sets up Milestone 9's required limitations analysis (does the dataset have enough labeled outcomes per
  bucket to support real statistics), which explicitly builds on this milestone's dataset.

## 3. Implementation Steps (in order)

1. **Add `Evaluation/EvalScenario.cs`**: a scenario record for the non-branching case (initial description,
   expected material section ID(s), acceptable final `SourceSupport` set, expected minimum/maximum turns) and
   a branching variant with a single branch point (judge's answer text per branch, expected relevant section
   ID(s) after that answer, acceptable final `SourceSupport` set per branch) — one branch level, matching PRD
   §15's own worked example, not a general recursive tree (no demonstrated need for deeper nesting yet).
2. **Add `Evaluation/EvalDataset.cs`**: hand-authored cases covering PRD §15's required categories, including
   at least two branching cases reusing the missed-Prize and Special Condition scenarios already understood
   from Milestones 6-7's real runs.
3. **Add `Evaluation/ScenarioEvalRunner.cs`**: drives the real `ClarificationLoop` for a scenario with a
   scripted `askJudge` (returns the branch's pre-authored answer instead of reading console input), captures
   the full trajectory (each turn's retrieved chunks, sufficiency result, and any clarifying question asked),
   then — if sufficiency was reached — runs the real `RulingGenerator` and `GroundingValidator`, and returns
   everything the scorer needs. If the loop asks an unexpected clarifying question with no matching branch,
   record that as a scoring signal (a real, informative outcome), not a crash.
4. **Add `Evaluation/ScenarioEvalScorer.cs`**: pure, deterministic functions comparing a captured trajectory
   against a scenario's expected criteria — sufficiency-timing correctness, clarifying-question materiality
   (via `RelatedChunkId`), per-turn retrieval hit/miss (via `RetrievalEvaluator.Evaluate`), and final
   Source-Support/citation acceptability. Returns a structured pass/fail-with-reasons result per criterion,
   not just one boolean per scenario — this is the piece that gets the most unit tests, mirroring Milestone
   7's `SourceSupportAssigner` (deterministic combinator, thoroughly tested; the thing it scores comes from
   live model behavior and is exercised manually).
5. **Wire `dotnet run -- evaluate`** in `Program.cs`: runs every scenario in `EvalDataset` against the real
   corpus/model, printing each scenario's per-criterion breakdown and a final summary (N/M scenarios fully
   passed, plus which specific criteria failed where they did) — the "simple run log" this milestone owes
   PRD §15.
6. **Run the real harness** against the real corpus and capture the output.
7. **Update/add tests**: `ScenarioEvalScorerTests` (pure, thorough, table-driven per criterion — the
   deterministic core of this milestone), `ScenarioEvalRunnerTests` (stub-based, mirroring
   `ClarificationLoopTests`/`GroundingValidatorTests`'s established pattern — verifying the runner correctly
   drives multi-turn scenarios and assembles a trajectory, not verifying real model quality).
8. **Document observed findings** in `observed-limitations.md`: the real pass/fail breakdown, at least one
   case where a criterion's crude/keyword-based matching produced a questionable score (expected, given no
   LLM-judge exists here), and an honest note on dataset size.

## 4. Expected Limitations / Failures to Intentionally Observe

- **Scoring "was the right clarifying question asked" via `RelatedChunkId` is a structural proxy, not a
  semantic one.** It confirms the question was tied to a material section, not that the question's actual
  phrasing was good judge-facing language. Worth observing at least one case where this proxy and a human's
  own read of the question disagree.
- **This is a small, hand-authored dataset with no statistical claim attached.** A handful of scenarios per
  category catches gross regressions; it says nothing about the system's error rate in general. This is the
  exact limitation Milestone 9 is required to grapple with explicitly — worth stating plainly here rather than
  implying more rigor than exists.
- **The harness makes real LLM and embedding calls per scenario** (more for branching scenarios, which run
  the loop twice), on top of everything Milestones 6-7 already cost per ruling — real, cumulative latency/cost
  across a full dataset run, consistent with this project's running theme of not optimizing this away until
  it's a demonstrated problem.
- **No LLM-as-judge scoring of ruling quality/prose.** Deliberately not built — this project has direct,
  recent evidence (Milestone 7) that an LLM checking LLM output is not independent verification; adding a
  second, unvalidated LLM grader here would repeat that exact mistake at the evaluation layer instead of
  learning from it.

## 5. What I Should Understand by the End

- The concrete difference between "the final ruling was correct" and "the investigation that produced it was
  correct," and why a hand-authored branching scenario is what makes that distinction checkable at all.
- Which of PRD §15's evaluation metrics are fully deterministic given a captured trajectory, and which remain
  structural proxies for something genuinely qualitative.
- Why this milestone deliberately does not build an LLM-as-judge quality scorer, connected directly to what
  Milestone 7 already demonstrated about self-validation.
- Why a small, hand-authored dataset is an honest, named limitation rather than a stepping stone assumed to be
  "good enough" without saying so — and what Milestone 9 will need to do with it.

## Out of Scope for This Milestone

- Numeric/calibrated confidence scores or collecting model-reported correctness probabilities — Milestone 9,
  which explicitly builds on this milestone's dataset rather than being part of it.
- LLM-as-judge scoring of free-form ruling quality — not planned; PRD does not call for it, and Milestone 7's
  findings argue against adopting it casually.
- A persisted, historical eval-report/dashboard system — PRD asks for "simple run logs/reports"; this
  milestone's `observed-limitations.md` fills that role, matching every prior milestone's practice.
- General recursive/multi-level branching-scenario trees — a single branch point per scenario matches PRD
  §15's own example; deeper trees are deferred until a concrete scenario actually needs one.
- Any retrieval, chunking, or ingestion improvements — this milestone measures the existing pipeline, it
  doesn't change it.
- Any UI work (Milestone 10).
