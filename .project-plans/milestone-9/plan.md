# Milestone 9 — Confidence Calibration and Reliability

Status: implemented; live experiment run — see implementation-summary.md and calibration-analysis.md
Source: PRD.md §9 ("Reliability and Safety Requirements"), §12 (tech-stack table), §15 ("Evaluation dataset
hardening (Milestone 8.5)" — Milestone 9 dependency), §17 (Success Criteria), §18 (Open Questions), Learning
Objectives → "Milestone 9 — Confidence Calibration and Reliability". Unlike Milestone 8.5, Milestone 9 has no
single dedicated PRD subsection of its own — its scope is defined by the Learning Objectives entry plus these
scattered, consistent references, all read together for this plan.

## 1. What We Will Build

Milestone 8 built the eval harness; Milestone 8.5 hardened the dataset it measures with. Milestone 9 is the
first milestone to actually *use* that hardened dataset for its stated purpose: investigating whether
PokéJudge can produce a meaningful, empirically validated estimate of how likely a ruling is to be correct —
and, just as importantly, honestly assessing whether the dataset is even large enough to answer that question.
This is explicitly exploratory (PRD: "an advanced, exploratory topic, not a requirement for a working
product") — the qualitative Source Support signal (Strong/Partial/Insufficient) remains the default
judge-facing reliability signal throughout, and stays that way unless this milestone's evidence justifies
otherwise.

1. **Have the model self-report a predicted correctness probability**, separate from and in addition to the
   existing Source Support classification. PRD §9 is explicit that these are philosophically distinct
   signals — "Confidence describes belief; Source Support describes evidence" — so this is a new signal, not a
   replacement or reframing of Source Support.
2. **Build a small, deterministic calibration-analysis module** that compares self-reported confidence against
   actual outcomes from the hardened Milestone 8.5 dataset (run with `--repeat`, so each repeat is its own
   independent observation, not collapsed into one label per scenario) — bucketing predictions into ranges
   (e.g. 50–60%, 60–70%, ... 90–100%) and checking whether observed correctness in each bucket matches the
   stated range.
3. **Compute calibration statistics scoped to what the data actually supports — sized against real numbers,
   not decided after the fact.** A fine-grained reliability diagram/ECE (5–10 buckets) needs roughly 30+
   observations per bucket to be stable (~200–300+ total) — well beyond what this dataset can produce even at
   a generous repeat count. The realistic target, planned for from the start rather than discovered as a
   fallback: `--repeat 5` across the full 20-scenario dataset (~100 attempted observations), expecting
   **~70–90 usable observations** after known crashes/turn-cap-exhaustions/infrastructure failures remove
   their share. That supports a **2–3 coarse bucket comparison and a Brier score** (both workable at that
   scale), but not a defensible fine-grained ECE — the analysis targets those two, and reports ECE only if the
   real yield turns out larger than expected, not as the primary goal.
4. **Compare self-reported confidence against other reliability signals already produced by the pipeline** —
   retrieval quality (`ScoredChunk.Score`), citation coverage (`GroundingResult.AllCitationsExist`), the
   Source Support classification itself, explicit-vs-inferred policy support
   (`GroundingAssessment.Citations[].SupportLevel`), source conflict (`GroundingAssessment.ConflictDetected`),
   and evaluation performance on similar past scenarios (cross-referenced from `five-run-validation.md` and
   `SourceCoverageFindings`) — and investigate, as an exploratory comparison rather than a new automated
   scoring criterion, whether combining these signals looks more informative than self-reported confidence
   alone.
5. **Write the required limitations analysis** as its own deliverable: how many labeled outcomes fall into
   each probability bucket, an honest assessment of whether that sample size supports the chosen statistic,
   and explicit accounting for Milestone 8.5's repeated-run observations (a scenario with different outcomes
   across runs has no single fixed ground-truth label) and source-coverage uncertainty (don't blame the
   confidence signal for what a source-coverage finding already explained).
6. **Make the end-of-milestone product decision explicitly, based on the evidence gathered** — not assumed in
   advance: if the reliability score demonstrates adequate calibration, consider exposing it to judges
   (design only, since UI itself is Milestone 10); if it doesn't, retain Source Support as the sole
   judge-facing signal and keep the numeric confidence work internal to evaluation.

## 2. AI Concepts Being Learned

- **The difference between model confidence and actual correctness**, and why that's a fundamentally
  different property from accuracy — a model can be highly accurate but poorly calibrated (systematically
  over- or under-confident), or reasonably calibrated while still frequently wrong.
- **What calibration means, concretely**, and how it's measured: reliability diagrams/calibration curves,
  Expected Calibration Error, Brier Score — and, just as important, recognizing when a dataset is too small
  for a given statistic to mean anything, rather than computing it anyway because the formula runs on any
  number of inputs.
- **A raw model-reported number is not a probability until it's checked against real outcomes.** This
  milestone's entire premise is that "the model said 85%" is just more model output text — a claim, not a
  calibrated estimate — until it's compared against the hardened dataset's actual, repeated-run outcomes.
- **Why Source Support and self-reported confidence are architecturally kept separate**, reinforced by
  building the second signal now: Source Support is validated against observable, deterministic criteria
  (retrieval success, citation coverage, fact sufficiency — see `SourceSupportAssigner`); self-reported
  confidence is pure model belief with no validation step at all until this milestone's analysis checks it
  against real outcomes.
- **Distinguishing exploratory evaluation work from a product decision.** This milestone investigates and
  reports; it does not, by itself, decide to expose a number to judges — that's a separate, evidence-gated
  decision made at the end, consistent with PRD §18's explicit deferral.

## 3. Implementation Steps (in order)

1. **Add a new `ConfidenceEstimator` class** (`Reliability/ConfidenceEstimator.cs`), mirroring
   `GroundingValidator`'s existing shape: takes the already-generated `RulingResult` and the retrieved chunks
   and makes a **separate LLM call** asking the model to state a predicted probability (0–100) that its own
   ruling is correct, plus a short rationale. This is deliberately its own step, not a field added onto
   `RulingResult` alongside `SourceSupport` — the same reasoning Milestone 6 already established for keeping
   ruling generation and sufficiency assessment separate calls: a self-assessment of an already-produced
   ruling is a genuinely different task from producing the ruling, and keeping it a distinct call makes the
   two signals (Source Support, self-reported confidence) produced independently enough to meaningfully
   compare, not two fields of one generation pass.
   **Deliberately excludes the grounding/Source Support result from its input** (confirmed during
   implementation, not just at planning time): if the confidence-estimation prompt saw
   `GroundingResult.ValidatedSourceSupport` before estimating, the model could simply restate that
   classification as a percentage, making the milestone's central confidence-vs-Source-Support comparison
   circular rather than informative. `ConfidenceEstimator` sees only what `RulingGenerator` saw (scenario,
   confirmed facts, retrieved passages) plus its own already-produced ruling — never the downstream
   validation step's output.
2. **Add `ConfidenceEstimateSchema`/`SystemPrompts.ConfidenceEstimation`**, following the existing
   hand-written-schema-mirrors-record pattern (`RulingResultSchema`, `GroundingAssessmentSchema`). The prompt
   must be explicit that this is a probability the *ruling itself* is correct, not a restatement of Source
   Support and not a general-purpose confidence-in-anything score — precise instruction matters here, since
   this whole milestone depends on the number meaning one specific thing consistently across scenarios.
3. **Wire `ConfidenceEstimator` into `ScenarioEvalRunner`** (real-run capture) and, separately, into
   `Program.cs`'s interactive console flow for parity with the rest of the pipeline — but note explicitly in
   code comments that this is an *internal, evaluation-facing* signal, never printed to the judge-facing
   summary output, consistent with PRD §9's "must not display... unless empirically validated as calibrated."
4. **Build a small, pure `Reliability/CalibrationAnalysis.cs` module** (no LLM calls): given a list of
   `(PredictedProbability, ActualCorrect, ScenarioId, Category)` observations, bucket them into ranges and
   compute observed-correctness-per-bucket; add ECE/Brier calculators as separate, independently testable
   functions so a bucket table can be produced even if the sample size can't support a summary statistic yet.
   `ActualCorrect` is derived from the existing, unchanged `ScenarioEvalScorer.Score(...).AllPassed` — this
   milestone does not touch scoring criteria, it consumes what Milestone 8/8.5 already established as ground
   truth. Each observation also carries the **full `ScenarioEvalReport.Criteria` breakdown**, not just the
   collapsed boolean — a wrong prediction should be traceable to *which* criterion failed (retrieval,
   materiality, Source Support, etc.), not just flagged as "wrong," so a miscalibration finding can say
   something about its cause rather than only its existence.
5. **Add a `dotnet run -- calibrate [--from/--only/--repeat]` command**, reusing `EvalScenarioSelector` and
   the existing `ScenarioEvalRunner`/`ScenarioEvalScorer` pipeline exactly as `evaluate` does, but additionally
   capturing each run's `ConfidenceEstimator` output and feeding the full set into `CalibrationAnalysis` at the
   end, printing the bucket table (and ECE/Brier if the data supports it) instead of `evaluate`'s per-category
   pass/fail summary. Kept as a separate command rather than a flag on `evaluate` so `evaluate`'s existing,
   already-relied-upon output format is untouched.
6. **Manually compare self-reported confidence against the other available reliability signals** for a sample
   of real runs — a human judgment call, deliberately not a new automated scoring criterion or an
   LLM-as-judge check, consistent with this project's standing position (Milestone 7's grounding-analysis.md,
   Milestone 8.5's source-coverage classification). Written up narratively, not computed.
7. **Run the real experiment**: execute `calibrate --repeat 5` across the hardened 20-scenario dataset against
   the live model and real corpus, mindful of the free-tier rate limit Milestone 8.5 already documented
   (pacing/`--from`/`--only` as needed). Capture real predicted-probability/actual-correctness pairs — not
   synthetic or assumed data. Expect roughly 100 attempted observations and ~70–90 usable ones after known
   crashes/turn-cap-exhaustions/infrastructure failures are excluded, per step 3's sizing above.
8. **Tag observations from `missed-prize` and `mulligan-not-taken` explicitly** in the captured data (both
   already have documented, unresolved Milestone 8.5 findings unrelated to confidence calibration — a possible
   source gap and multi-path variability, respectively). Run `CalibrationAnalysis` both with and without these
   two scenarios' observations included, so a known, already-explained issue doesn't get misread as new
   evidence about confidence calibration.
9. **Write the required limitations analysis** (`.project-plans/milestone-9/calibration-analysis.md`):
   per-bucket sample counts, an honest verdict on whether ECE/Brier is meaningful at this sample size or
   whether only the planned coarser comparison is defensible, how repeated-run observations were incorporated
   (each repeat as its own data point, never collapsed), and how step 8's tagged-scenario comparison changed
   (or didn't change) the picture.
10. **Make and document the end-of-milestone product decision** explicitly, in `implementation-summary.md`:
    given the actual calibration evidence, does a numeric reliability estimate look trustworthy enough to be
    worth considering for judges (design intent only — no UI work happens here), or does Source Support remain
    the sole judge-facing signal with confidence work staying internal to evaluation?
11. **Update/add tests**: unit tests for `CalibrationAnalysis`'s bucketing and ECE/Brier calculators
    (deterministic, hand-constructed fixtures — no LLM involved, matching this project's established
    deterministic-vs-probabilistic testing split); tests for the new `ConfidenceEstimateSchema`
    round-trip/deserialization shape, mirroring existing schema tests. No changes anticipated to
    `ScenarioEvalScorer`'s criteria or `ClarificationLoop` — this milestone adds a new, independent signal and
    an analysis layer over existing outcomes, it does not change how those outcomes are produced or scored.
12. **Document findings** in `.project-plans/milestone-9/implementation-summary.md` and the calibration
    analysis document above.

## 4. Expected Limitations / Failures to Intentionally Observe

- **Per-bucket sample sizes are too small for a fine-grained ECE/reliability diagram, by design of the
  dataset's own size, not an accident.** A defensible fine-grained picture needs ~30+ observations per bucket
  (~200–300+ total); the realistic yield here is ~70–90 usable observations from `--repeat 5` across 20
  scenarios (see §1's sizing) — enough for a 2–3 coarse bucket comparison and a Brier score, not enough for a
  trustworthy ECE at fine granularity. Planned for from the start (step 3/7), not discovered as a surprise
  during the analysis.
- **LLM self-reported confidence commonly clusters in a narrow, high range** (a well-documented general
  overconfidence pattern in LLM systems) — expect predicted probabilities to bunch toward 70–95% regardless of
  actual outcome, which would itself under-populate the lower buckets and further limit what the bucket table
  can show, on top of the sample-size ceiling above.
- **`ActualCorrect` alone folds several different criteria into one boolean** (retrieval, sufficiency timing,
  materiality, answer budget, Source Support) — mitigated by step 4's decision to also capture the full
  `ScenarioEvalReport.Criteria` breakdown per observation, but a miscalibration finding may still span
  multiple contributing criteria at once and not reduce to one clean cause.
- **Repeated-run variability (established in Milestone 8.5) means "ground truth" isn't always a fixed label
  per scenario.** A scenario like `deck-not-shuffled` or `mulligan-not-taken` can have genuinely different
  outcomes across otherwise-identical runs — each repeat must be treated as its own independent observation
  in the calibration set, not aggregated into one answer per scenario, or the analysis would be built on a
  false premise the project already disproved.
- **Known, still-open Milestone 8.5 findings will inject noise, not signal, into this dataset** —
  `missed-prize`'s possible source gap and `mulligan-not-taken`'s three-distinct-trajectories problem are
  likely to produce confidence/correctness pairs that reflect those known, already-documented issues rather
  than anything new about calibration. Mitigated by step 8's explicit tagging and with/without comparison,
  rather than silently averaging them into the primary result.

## 5. What I Should Understand by the End

- The difference between calibration and accuracy, and why a model can fail at one without failing at the
  other.
- Why a self-reported probability is not a validated estimate until checked against real, independently
  scored outcomes — and why that check is exactly what this milestone builds.
- How to recognize when a dataset's sample size can't support a given statistic, and what to do instead
  (report the limitation plainly, use a coarser comparison) rather than computing a precise-looking number
  regardless.
- Why Source Support (evidence-based, criteria-validated) and self-reported confidence (belief-based,
  unvalidated until checked) are kept as architecturally separate signals, and what that separation buys the
  product even before this milestone's evidence comes in.
- Why "combining signals looks promising" is an exploratory finding to report, not a new production feature to
  ship in this milestone — and what would actually need to be true before it became one.

## Out of Scope for This Milestone

- Any UI work exposing a numeric reliability estimate to judges — Milestone 10, and only if this milestone's
  evidence justifies it at all.
- Changing `ScenarioEvalScorer`'s scoring criteria, `ClarificationLoop`, or how `ActualCorrect`/ground truth is
  determined — this milestone consumes what Milestone 8/8.5 already established, it doesn't redefine it.
- A production "combined reliability score" that merges self-reported confidence with the other signals into
  one number — investigated and reported on qualitatively, not built as a shipped feature.
- Broad source-corpus or eval-dataset expansion — out of scope here exactly as it was for Milestone 8.5;
  Milestone 9 uses the dataset as hardened, it doesn't grow it further.
- General-purpose calibration tooling or an off-the-shelf calibration library — hand-rolled and scoped to this
  project's actual dataset size, consistent with the project's stated intent to build the full pipeline from
  first principles rather than adopt a framework.
- Retrying or otherwise smoothing over the known, still-open Milestone 8.5 findings (`missed-prize`,
  `mulligan-not-taken`) — they're inputs to this milestone's limitations analysis, not something this
  milestone is meant to resolve.
